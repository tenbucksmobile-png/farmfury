import { View, ActivityIndicator } from 'react-native';
import { Redirect } from 'expo-router';
import { useEmployee } from '@/providers/EmployeeContext';

/**
 * Root index — defers routing to AuthProvider.
 *
 * Shows a spinner while the employee session is being rehydrated from
 * AsyncStorage. Once isLoaded is true, AuthProvider's useEffect fires and
 * redirects to the correct route:
 *   employee set  → /(tabs)
 *   employee null → /(auth)/employee-auth
 */
export default function Index() {
  const { isLoaded } = useEmployee();

  if (!isLoaded) {
    return (
      <View style={{ flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: '#ffffff' }}>
        <ActivityIndicator size="large" color="#7B1FA2" />
      </View>
    );
  }

  // AuthProvider handles the redirect once isLoaded; this is a safe fallback.
  return <Redirect href="/(auth)/employee-auth" />;
}
