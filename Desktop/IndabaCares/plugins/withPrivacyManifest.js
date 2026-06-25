/**
 * withPrivacyManifest.js
 *
 * Injects PrivacyInfo.xcprivacy into the iOS native project at prebuild time.
 *
 * Required by Apple as of May 1 2024 for all App Store submissions.
 * Covers every "required reason" API used directly or via SDK in this project:
 *
 *   NSPrivacyAccessedAPICategoryUserDefaults
 *     CA92.1 — used by AsyncStorage, SecureStore, Reanimated, Notifications
 *
 *   NSPrivacyAccessedAPICategoryFileTimestamp
 *     C617.1 — used by expo-image (disk cache), expo-file-system
 *
 *   NSPrivacyAccessedAPICategoryDiskSpace
 *     E174.1 — used by expo-image (cache eviction policy)
 *
 *   NSPrivacyAccessedAPICategorySystemBootTime
 *     35F9.1 — used by react-native-reanimated (animation timing)
 */

const { withDangerousMod } = require('@expo/config-plugins');
const fs = require('fs');
const path = require('path');

const PRIVACY_MANIFEST = `<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <!-- No tracking — app does not track users across apps/websites -->
  <key>NSPrivacyTracking</key>
  <false/>

  <!-- No tracking domains -->
  <key>NSPrivacyTrackingDomains</key>
  <array/>

  <!-- No data collected from this app linked to user identity -->
  <key>NSPrivacyCollectedDataTypes</key>
  <array/>

  <!-- Required reason API declarations -->
  <key>NSPrivacyAccessedAPITypes</key>
  <array>

    <!-- UserDefaults — AsyncStorage, SecureStore, Reanimated, Notifications -->
    <dict>
      <key>NSPrivacyAccessedAPIType</key>
      <string>NSPrivacyAccessedAPICategoryUserDefaults</string>
      <key>NSPrivacyAccessedAPITypeReasons</key>
      <array>
        <string>CA92.1</string>
      </array>
    </dict>

    <!-- File timestamp — expo-image disk cache -->
    <dict>
      <key>NSPrivacyAccessedAPIType</key>
      <string>NSPrivacyAccessedAPICategoryFileTimestamp</string>
      <key>NSPrivacyAccessedAPITypeReasons</key>
      <array>
        <string>C617.1</string>
      </array>
    </dict>

    <!-- Disk space — expo-image cache eviction -->
    <dict>
      <key>NSPrivacyAccessedAPIType</key>
      <string>NSPrivacyAccessedAPICategoryDiskSpace</string>
      <key>NSPrivacyAccessedAPITypeReasons</key>
      <array>
        <string>E174.1</string>
      </array>
    </dict>

    <!-- System boot time — react-native-reanimated animation timing -->
    <dict>
      <key>NSPrivacyAccessedAPIType</key>
      <string>NSPrivacyAccessedAPICategorySystemBootTime</string>
      <key>NSPrivacyAccessedAPITypeReasons</key>
      <array>
        <string>35F9.1</string>
      </array>
    </dict>

  </array>
</dict>
</plist>
`;

module.exports = function withPrivacyManifest(config) {
  return withDangerousMod(config, [
    'ios',
    (config) => {
      const iosDir = path.join(config.modRequest.platformProjectRoot);
      const appDir = path.join(iosDir, config.modRequest.projectName);
      const dest   = path.join(appDir, 'PrivacyInfo.xcprivacy');

      fs.mkdirSync(appDir, { recursive: true });
      fs.writeFileSync(dest, PRIVACY_MANIFEST, 'utf8');

      console.log('[withPrivacyManifest] PrivacyInfo.xcprivacy written to', dest);
      return config;
    },
  ]);
};
