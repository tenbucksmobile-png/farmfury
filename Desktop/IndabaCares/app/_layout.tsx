import '../global.css';
import React from 'react';
import { Slot } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { GestureHandlerRootView } from 'react-native-gesture-handler';
import { SafeAreaProvider } from 'react-native-safe-area-context';
import { QueryProvider } from '@/providers/QueryProvider';
import { EmployeeProvider } from '@/providers/EmployeeContext';
import { AuthProvider } from '@/providers/AuthProvider';
import { RealtimeProvider } from '@/providers/RealtimeProvider';
import { NotificationProvider } from '@/providers/NotificationProvider';
import { ToastProvider } from '@/providers/ToastProvider';
import { ErrorBoundary } from '@/components/ErrorBoundary';
import {
  useFonts,
  DancingScript_700Bold,
} from '@expo-google-fonts/dancing-script';
import { Ionicons } from '@expo/vector-icons';

export default function RootLayout() {
  const [fontsLoaded] = useFonts({
    DancingScript_700Bold,
    ...Ionicons.font,
  });

  // Render app whether or not fonts have loaded — system font fallback used until ready
  return (
    <ErrorBoundary>
      <GestureHandlerRootView style={{ flex: 1 }}>
        <SafeAreaProvider>
          <QueryProvider>
            <EmployeeProvider>
              <AuthProvider>
                <RealtimeProvider>
                  <NotificationProvider>
                    <ToastProvider>
                      <StatusBar style="dark" />
                      <Slot />
                    </ToastProvider>
                  </NotificationProvider>
                </RealtimeProvider>
              </AuthProvider>
            </EmployeeProvider>
          </QueryProvider>
        </SafeAreaProvider>
      </GestureHandlerRootView>
    </ErrorBoundary>
  );
}
