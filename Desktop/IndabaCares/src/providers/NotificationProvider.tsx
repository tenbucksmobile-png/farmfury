import React, { useEffect, useRef } from 'react';
import { Platform } from 'react-native';
import Constants from 'expo-constants';
import { useEmployee } from '@/providers/EmployeeContext';
import { supabase } from '@/lib/supabase';
import { routeFromNotification } from '@/utils/notification-router';
import type { NotificationType } from '@/types/database';
// NOTIF_PERMISSION_KEY lives in constants — import from there; do not re-declare here.
export { NOTIF_PERMISSION_KEY } from '@/lib/constants';

// ─── Lazy loader ──────────────────────────────────────────────────────────────
//
// expo-notifications accesses RequireNativeModule('ExpoBadgeModule') at module-
// eval time on the Hermes background thread → void TurboModule dispatch → throws
// NSException → convertNSExceptionToJSError races with JS thread → SIGBUS.
//
// Pattern from fizzog Build 25–26: defer the entire require() behind a 3-second
// setTimeout so the first access always happens on the main JS thread, safely
// post-render after TurboModules are fully registered.

let _Notifications: typeof import('expo-notifications') | null = null;
function _loadNotifications() {
  if (!_Notifications) _Notifications = require('expo-notifications');
  return _Notifications;
}

// ─── Token registration (called when permission is already granted) ──────────

async function registerTokenIfGranted(
  employeeId: string,
  hotel:      string,
  attempt     = 1,
): Promise<void> {
  const MAX_RETRIES    = 3;
  const RETRY_DELAY_MS = 2000;

  try {
    if (Platform.OS === 'web') return;

    const N = _loadNotifications();
    const { status } = await N.getPermissionsAsync();
    if (status !== 'granted') return; // Permission not yet granted — pre-permission screen will handle this

    const projectId = Constants.expoConfig?.extra?.eas?.projectId;
    if (!projectId) return;

    const tokenData = await N.getExpoPushTokenAsync({ projectId });

    const { error } = await supabase.rpc('upsert_push_token', {
      p_employee_id: employeeId,
      p_hotel:       hotel,
      p_token:       tokenData.data,
      p_platform:    Platform.OS,
    });

    if (error) throw error;
  } catch (err) {
    if (attempt < MAX_RETRIES) {
      await new Promise((resolve) => setTimeout(resolve, RETRY_DELAY_MS * attempt));
      return registerTokenIfGranted(employeeId, hotel, attempt + 1);
    }
    console.warn('[Notifications] Token registration failed after retries:', err);
  }
}

// ─── Provider ─────────────────────────────────────────────────────────────────

export function NotificationProvider({ children }: { children: React.ReactNode }) {
  const { employee }         = useEmployee();
  const notificationListener = useRef<{ remove: () => void } | undefined>(undefined);
  const responseListener     = useRef<{ remove: () => void } | undefined>(undefined);

  // 3-second deferred init — matches fizzog Build 25-26 fix.
  // setNotificationHandler dispatches to the native notification module;
  // on iOS 26 this must not fire before TurboModules are fully registered.
  useEffect(() => {
    const timer = setTimeout(() => {
      _loadNotifications().setNotificationHandler({
        handleNotification: async () => ({
          shouldShowAlert:  true,
          shouldPlaySound:  true,
          shouldSetBadge:   true,
          shouldShowBanner: true,
          shouldShowList:   true,
        }),
      });
    }, 3000);
    return () => clearTimeout(timer);
  }, []);

  useEffect(() => {
    if (!employee) return;

    // Only attempt token registration if permission is already granted.
    // First-time permission request is handled by (screens)/notification-permission.tsx
    // which is navigated to after first login (see employee-auth.tsx).
    registerTokenIfGranted(employee.employee_id, employee.hotel);

    const N = _loadNotifications();

    notificationListener.current = N.addNotificationReceivedListener(
      (_notification) => {
        // Foreground handling managed by setNotificationHandler above.
        // Feed / badge updates driven by realtime subscriptions.
      }
    );

    responseListener.current = N.addNotificationResponseReceivedListener(
      (response) => {
        const data = response.notification.request.content.data as {
          type?: NotificationType;
          referenceType?: string;
          referenceId?: string;
        };
        if (data) {
          routeFromNotification({
            type:          data.type || 'system',
            referenceType: data.referenceType,
            referenceId:   data.referenceId,
          });
        }
      }
    );

    return () => {
      notificationListener.current?.remove();
      responseListener.current?.remove();
    };
  }, [employee?.employee_id]);

  return <>{children}</>;
}
