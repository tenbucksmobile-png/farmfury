import React, { useEffect } from 'react';
import { useRouter, useSegments } from 'expo-router';
import { useEmployee } from '@/providers/EmployeeContext';

/**
 * AuthProvider — route guard for the employee authentication system.
 *
 * States:
 *   1. Context not yet loaded from SecureStore → wait (do nothing)
 *   2. No authenticated employee → /(auth)/employee-auth
 *   3. Employee authenticated, has_seen_welcome still loading → wait
 *   4. Employee authenticated, has_seen_welcome = false → mark seen + go to /(tabs)
 *   5. Employee authenticated, has_seen_welcome = true → /(tabs)
 *
 * Note: the notification permission screen (/(screens)/notification-permission)
 * is navigated to explicitly after first login in employee-auth.tsx.
 * AuthProvider does not intercept that route.
 */
export function AuthProvider({ children }: { children: React.ReactNode }) {
  const router   = useRouter();
  const segments = useSegments();
  const { employee, isLoaded, hasSeenWelcome, markWelcomeSeen } = useEmployee();

  useEffect(() => {
    if (!isLoaded) return; // wait for SecureStore rehydration

    const inAuthGroup    = segments[0] === '(auth)';
    const inScreensGroup = segments[0] === '(screens)';
    const screen         = segments[1] as string;
    const onNotifScreen  = inScreensGroup && screen === 'notification-permission';
    const onPolicyScreen = inScreensGroup && (screen === 'privacy-policy' || screen === 'terms-of-service');

    if (!employee) {
      if (!inAuthGroup && !onPolicyScreen) router.replace('/(auth)/employee-auth');
      return;
    }

    // Authenticated — wait for has_seen_welcome to resolve before routing
    if (hasSeenWelcome === null) return;

    // First-time employee — mark welcome seen and go straight to main app
    if (!hasSeenWelcome && !onNotifScreen) {
      markWelcomeSeen();
      router.replace('/(tabs)/profile');
      return;
    }

    // Returning employee — send to main app if still in auth
    if (inAuthGroup) router.replace('/(tabs)/profile');
  }, [employee, isLoaded, hasSeenWelcome, markWelcomeSeen, segments, router]);

  return <>{children}</>;
}
