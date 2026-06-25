/**
 * Fixes two bugs in expo-asset@12.0.12 on iOS 26 New Architecture (Bridgeless):
 *
 * 1. ExpoAsset.js — lazy Proxy has no try/catch: if requireNativeModule('ExpoAsset')
 *    throws (TurboModule not yet registered), _assetModule stays null and every
 *    subsequent call also throws → all local require() images fail silently.
 *    Fix: add try/catch + null guard; if unavailable return the URL directly from
 *    downloadAsync() (the asset is already in the app bundle, no real download needed).
 *
 * 2. Asset.js — iOS has no equivalent of the Android "already local" shortcut.
 *    Android marks drawable-name assets as downloaded=true immediately (line 155).
 *    iOS file:// URIs (embedded app bundle) also don't need downloading — they're
 *    already on device. Without this shortcut, Asset.fromModule() always calls
 *    downloadAsync() which calls ExpoAsset.downloadAsync() which may fail.
 *    Fix: if uri starts with 'file://' on iOS, set localUri=uri and downloaded=true.
 */

'use strict';

const fs = require('fs');
const path = require('path');

function applyFix(label, filePath, oldStr, newStr, alreadyPatchedStr) {
  try {
    if (!fs.existsSync(filePath)) {
      console.warn('[fix-expo-asset] ' + label + ': file not found at ' + filePath + ' — skipping');
      return;
    }
    const content = fs.readFileSync(filePath, 'utf8');
    if (content.includes(alreadyPatchedStr)) {
      console.log('[fix-expo-asset] ' + label + ': already patched, skipping');
      return;
    }
    if (content.includes(oldStr)) {
      fs.writeFileSync(filePath, content.replace(oldStr, newStr), 'utf8');
      console.log('[fix-expo-asset] ' + label + ': patch applied');
    } else {
      console.warn('[fix-expo-asset] ' + label + ': expected pattern not found — file may have changed');
    }
  } catch (err) {
    console.warn('[fix-expo-asset] ' + label + ': error during patch — ' + err.message);
  }
}

// ── Fix 1: ExpoAsset.js ────────────────────────────────────────────────────────

const expoAssetPath = path.join(
  __dirname, '..', 'node_modules', 'expo-asset', 'build', 'ExpoAsset.js'
);

const oldProxy =
`let _assetModule = null;
const AssetModule = new Proxy({}, {
    get(_, prop) {
        if (!_assetModule) _assetModule = requireNativeModule('ExpoAsset');
        return _assetModule[prop];
    }
});`;

const newProxy =
`let _assetModule = null;
let _assetModuleResolved = false;
const AssetModule = new Proxy({}, {
    get(_, prop) {
        if (!_assetModuleResolved) {
            _assetModuleResolved = true;
            try { _assetModule = requireNativeModule('ExpoAsset'); } catch (_e) {}
        }
        if (_assetModule) return _assetModule[prop];
        // ExpoAsset TurboModule unavailable on New Architecture — for embedded
        // file:// assets the URI is already local, return it directly.
        if (prop === 'downloadAsync') return (url) => Promise.resolve(url);
        return undefined;
    }
});`;

applyFix('ExpoAsset.js', expoAssetPath, oldProxy, newProxy, '_assetModuleResolved');

// ── Fix 2: Asset.js ───────────────────────────────────────────────────────────

const assetPath = path.join(
  __dirname, '..', 'node_modules', 'expo-asset', 'build', 'Asset.js'
);

const oldAndroidBlock =
`            if (Platform.OS === 'android' && !uri.includes(':') && (meta.width || meta.height)) {
                asset.localUri = asset.uri;
                asset.downloaded = true;
            }
            Asset.byHash[meta.hash] = asset;`;

const newAndroidBlock =
`            if (Platform.OS === 'android' && !uri.includes(':') && (meta.width || meta.height)) {
                asset.localUri = asset.uri;
                asset.downloaded = true;
            }
            // iOS embedded assets: file:// URI is already on device — skip downloadAsync().
            if (Platform.OS === 'ios' && uri.startsWith('file://')) {
                asset.localUri = asset.uri;
                asset.downloaded = true;
            }
            Asset.byHash[meta.hash] = asset;`;

applyFix('Asset.js', assetPath, oldAndroidBlock, newAndroidBlock, "Platform.OS === 'ios' && uri.startsWith('file://')");
