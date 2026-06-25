/**
 * Notification Permission Screen
 *
 * Shown once after first login (before the native OS permission prompt).
 * Explains the value of notifications so the user is primed to grant permission.
 * Stored in SecureStore to ensure it only shows once per installation.
 */

import React, { useState } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ActivityIndicator,
  TouchableOpacity,
  Platform,
} from 'react-native';
import { router } from 'expo-router';
import * as Notifications from 'expo-notifications';
import * as SecureStore from 'expo-secure-store';
import Constants from 'expo-constants';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { useEmployee } from '@/providers/EmployeeContext';
import { supabase } from '@/lib/supabase';
import { NOTIF_PERMISSION_KEY } from '@/lib/constants';

export { NOTIF_PERMISSION_KEY };

const PURPLE = '#7C3AED';

const BENEFITS = [
  { icon: 'star',           color: '#f59e0b', text: 'Get notified when a colleague recognises you' },
  { icon: 'gift',           color: '#22c55e', text: 'Know when your reward order is ready' },
  { icon: 'trophy',         color: '#8b5cf6', text: 'Find out when you earn a new badge' },
  { icon: 'megaphone',      color: '#3b82f6', text: 'Receive important hotel announcements' },
];

export default function NotificationPermissionScreen() {
  const { employee } = useEmployee();
  const [loading,    setLoading]    = useState(false);

  async function handleEnable() {
    setLoading(true);
    try {
      // Request native permission
      const { status } = await Notifications.requestPermissionsAsync();

      if (status === 'granted' && employee) {
        // Register and persist push token
        const projectId = Constants.expoConfig?.extra?.eas?.projectId;
        if (projectId) {
          try {
            const tokenData = await Notifications.getExpoPushTokenAsync({ projectId });
            await supabase.rpc('upsert_push_token', {
              p_employee_id: employee.employee_id,
              p_hotel:       employee.hotel,
              p_token:       tokenData.data,
              p_platform:    Platform.OS,
            });
          } catch {
            // Non-fatal: token can be registered on next launch
          }
        }
      }
    } finally {
      // Mark as asked regardless of permission outcome
      await SecureStore.setItemAsync(NOTIF_PERMISSION_KEY, 'true');
      setLoading(false);
      router.replace('/');
    }
  }

  async function handleSkip() {
    await SecureStore.setItemAsync(NOTIF_PERMISSION_KEY, 'true');
    router.replace('/');
  }

  return (
    <SafeAreaView style={s.safe} edges={['top', 'bottom']}>
      <View style={s.screen}>
        {/* Icon */}
        <View style={s.iconWrap}>
          <Ionicons name="notifications" size={56} color={PURPLE} />
        </View>

        <Text style={s.title}>Stay in the loop</Text>
        <Text style={s.subtitle}>
          Enable notifications so you never miss a recognition or reward update.
        </Text>

        {/* Benefits list */}
        <View style={s.benefits}>
          {BENEFITS.map(({ icon, color, text }) => (
            <View key={text} style={s.benefitRow}>
              <View style={[s.benefitIcon, { backgroundColor: color + '18' }]}>
                <Ionicons name={icon as any} size={18} color={color} />
              </View>
              <Text style={s.benefitText}>{text}</Text>
            </View>
          ))}
        </View>

        <Text style={s.note}>
          You can change this at any time in Settings → Notifications.
        </Text>

        {/* Actions */}
        <TouchableOpacity
          onPress={handleEnable}
          disabled={loading}
          activeOpacity={0.85}
          style={s.enableBtn}
        >
          {loading
            ? <ActivityIndicator color="#fff" />
            : <Text style={s.enableBtnText}>Enable Notifications</Text>
          }
        </TouchableOpacity>

        <TouchableOpacity onPress={handleSkip} style={s.skipBtn} disabled={loading}>
          <Text style={s.skipText}>Not Now</Text>
        </TouchableOpacity>
      </View>
    </SafeAreaView>
  );
}

const s = StyleSheet.create({
  safe:   { flex: 1, backgroundColor: '#fff' },
  screen: { flex: 1, paddingHorizontal: 28, justifyContent: 'center' },

  iconWrap: {
    width: 100, height: 100, borderRadius: 28,
    backgroundColor: '#ede9fe',
    alignSelf: 'center',
    alignItems: 'center', justifyContent: 'center',
    marginBottom: 28,
  },

  title:    { fontSize: 28, fontWeight: '800', color: '#1e1b4b', textAlign: 'center', marginBottom: 10 },
  subtitle: { fontSize: 15, color: '#64748b', textAlign: 'center', lineHeight: 22, marginBottom: 28 },

  benefits:    { gap: 14, marginBottom: 24 },
  benefitRow:  { flexDirection: 'row', alignItems: 'center' },
  benefitIcon: { width: 38, height: 38, borderRadius: 12, alignItems: 'center', justifyContent: 'center', marginRight: 14 },
  benefitText: { flex: 1, fontSize: 14, color: '#334155', lineHeight: 20 },

  note: { fontSize: 12, color: '#94a3b8', textAlign: 'center', marginBottom: 32 },

  enableBtn: {
    backgroundColor: PURPLE,
    borderRadius: 16,
    paddingVertical: 16,
    alignItems: 'center',
    marginBottom: 12,
    shadowColor: PURPLE,
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.3,
    shadowRadius: 10,
    elevation: 5,
  },
  enableBtnText: { fontSize: 16, fontWeight: '700', color: '#fff' },

  skipBtn:  { alignItems: 'center', paddingVertical: 12 },
  skipText: { fontSize: 14, fontWeight: '600', color: '#94a3b8' },
});
