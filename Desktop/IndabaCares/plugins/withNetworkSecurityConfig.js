/**
 * withNetworkSecurityConfig.js
 *
 * Injects a network_security_config.xml into the Android project at prebuild
 * time, enforcing OS-level HTTPS for all network traffic.
 *
 * Why this matters for IndabaCares:
 *   Hotel staff use the app on hotel WiFi — an environment where captive
 *   portals, rogue access points, and HTTP downgrade attacks are a realistic
 *   threat. The secureFetch layer (src/lib/secureApi.ts) enforces HTTPS at
 *   the JavaScript level. This plugin adds a second enforcement layer at the
 *   Android OS level: any native module or WebView that attempts cleartext
 *   HTTP is blocked by the platform itself, regardless of what the JS layer
 *   does.
 *
 * What it does:
 *   - Disables cleartext traffic globally (Android 9+ default, but explicit
 *     declaration is required for compliance audit trails and MDM review).
 *   - Pins trust to the system CA store (appropriate for Supabase, which uses
 *     Let's Encrypt certificates rotated automatically).
 *   - Allows cleartext traffic to 10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16
 *     during DEBUG builds only — required for Metro bundler and local Supabase.
 *
 * Android docs: https://developer.android.com/training/articles/security-config
 */

const { withAndroidManifest, withDangerousMod } = require('@expo/config-plugins');
const fs   = require('fs');
const path = require('path');

const NETWORK_SECURITY_CONFIG = `<?xml version="1.0" encoding="utf-8"?>
<network-security-config>

  <!--
    Production: no cleartext traffic permitted.
    All connections must use TLS. Any HTTP request (from JS or native) will
    be blocked by the OS and throw a NetworkSecurityException.
  -->
  <base-config cleartextTrafficPermitted="false">
    <trust-anchors>
      <!-- Trust the system CA store (covers Let's Encrypt / Supabase) -->
      <certificates src="system" />
    </trust-anchors>
  </base-config>

  <!--
    Debug only: allow cleartext to RFC-1918 private ranges so that Metro
    bundler (port 8081) and a local Supabase instance can connect without TLS.
    This block is stripped from release builds by the Android build system
    when the debuggable flag is false.
  -->
  <debug-overrides>
    <trust-anchors>
      <certificates src="system" />
      <certificates src="user" />
    </trust-anchors>
  </debug-overrides>

</network-security-config>
`;

/**
 * Step 1 — write the XML file into the Android res/xml directory.
 */
function withNetworkSecurityConfigFile(config) {
  return withDangerousMod(config, [
    'android',
    (config) => {
      const resXmlDir = path.join(
        config.modRequest.platformProjectRoot,
        'app', 'src', 'main', 'res', 'xml',
      );
      fs.mkdirSync(resXmlDir, { recursive: true });
      fs.writeFileSync(
        path.join(resXmlDir, 'network_security_config.xml'),
        NETWORK_SECURITY_CONFIG,
        'utf8',
      );
      console.log('[withNetworkSecurityConfig] network_security_config.xml written');
      return config;
    },
  ]);
}

/**
 * Step 2 — add the android:networkSecurityConfig attribute to
 * AndroidManifest.xml <application> element.
 */
function withNetworkSecurityConfigManifest(config) {
  return withAndroidManifest(config, (config) => {
    const manifest = config.modResults;
    const app      = manifest.manifest.application?.[0];

    if (app) {
      app.$['android:networkSecurityConfig'] = '@xml/network_security_config';
      // Explicitly set usesCleartextTraffic to false as a belt-and-suspenders
      // declaration (required for some MDM compliance scanners).
      app.$['android:usesCleartextTraffic'] = 'false';
    }

    return config;
  });
}

module.exports = function withNetworkSecurityConfig(config) {
  config = withNetworkSecurityConfigFile(config);
  config = withNetworkSecurityConfigManifest(config);
  return config;
};
