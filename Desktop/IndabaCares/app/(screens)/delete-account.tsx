import React, { useState } from 'react';
import {
  View,
  Text,
  TextInput,
  StyleSheet,
  ScrollView,
  Alert,
  ActivityIndicator,
  TouchableOpacity,
} from 'react-native';
import { Stack, router } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useEmployee } from '@/providers/EmployeeContext';
import { supabase } from '@/lib/supabase';

const PURPLE  = '#7C3AED';
const DANGER  = '#ef4444';

export default function DeleteAccountScreen() {
  const { employee, clearEmployee } = useEmployee();

  const [confirmText, setConfirmText] = useState('');
  const [password,    setPassword]    = useState('');
  const [loading,     setLoading]     = useState(false);
  const [error,       setError]       = useState<string | null>(null);

  const CONFIRM_PHRASE = 'DELETE MY ACCOUNT';
  const confirmed = confirmText.trim().toUpperCase() === CONFIRM_PHRASE;

  async function handleDelete() {
    if (!employee) return;
    if (!confirmed) {
      setError(`Please type "${CONFIRM_PHRASE}" exactly to confirm.`);
      return;
    }
    if (!password.trim()) {
      setError('Please enter your password to confirm deletion.');
      return;
    }

    Alert.alert(
      'Delete Account',
      'This will permanently delete your account and all personal data. This action cannot be undone.',
      [
        { text: 'Cancel', style: 'cancel' },
        {
          text: 'Delete Forever',
          style: 'destructive',
          onPress: performDeletion,
        },
      ],
    );
  }

  async function performDeletion() {
    if (!employee) return;
    setLoading(true);
    setError(null);

    try {
      // Step 1: Re-authenticate to confirm identity
      const { data: authData, error: authError } = await supabase.rpc(
        'authenticate_employee',
        {
          p_employee_code: employee.employee_code,
          p_hotel:         employee.hotel,
          p_password:      password,
        },
      );

      if (authError || !authData?.ok) {
        setError('Incorrect password. Please try again.');
        return;
      }

      // Step 2: Call deletion RPC
      const { data: deleteData, error: deleteError } = await supabase.rpc(
        'delete_employee_account',
        {
          p_employee_id: employee.employee_id,
          p_hotel:       employee.hotel,
        },
      );

      if (deleteError || !deleteData?.ok) {
        setError(deleteData?.error ?? 'Deletion failed. Please contact support.');
        return;
      }

      // Step 3: Clear local session
      await clearEmployee();

      // Navigation happens automatically via AuthProvider after clearEmployee
    } catch {
      setError('Something went wrong. Please try again or contact support at privacy@indabacares.com');
    } finally {
      setLoading(false);
    }
  }

  return (
    <SafeAreaView style={s.safe} edges={['top']}>
      <Stack.Screen options={{ headerShown: false }} />

      <View style={s.header}>
        <TouchableOpacity onPress={() => router.back()} style={s.backBtn} hitSlop={12}>
          <Ionicons name="chevron-back" size={22} color="#fff" />
        </TouchableOpacity>
        <Text style={s.headerTitle}>Delete Account</Text>
      </View>

      <ScrollView
        style={s.scroll}
        contentContainerStyle={s.content}
        keyboardShouldPersistTaps="handled"
        showsVerticalScrollIndicator={false}
      >
        {/* Warning banner */}
        <View style={s.warnBox}>
          <Ionicons name="warning" size={28} color={DANGER} style={{ marginBottom: 10 }} />
          <Text style={s.warnTitle}>This cannot be undone</Text>
          <Text style={s.warnBody}>
            Deleting your account will permanently remove your personal data,
            session, push tokens, mood history, and reaction history.{'\n'}
            {'\n'}
            Your recognition history will be anonymised. Any pending rewards
            orders will be cancelled and points refunded.{'\n'}
            {'\n'}
            Approved or fulfilled reward orders are retained for fulfilment records.
          </Text>
        </View>

        {/* What is deleted */}
        <View style={s.card}>
          <Text style={s.cardTitle}>What gets deleted</Text>
          {[
            'Your name and employee code',
            'All active sessions and push tokens',
            'Daily mood check-ins',
            'Reaction and comment history',
            'Pending reward orders (points refunded)',
            'Your profile photo',
          ].map((item) => (
            <View key={item} style={s.bulletRow}>
              <Ionicons name="close-circle" size={16} color={DANGER} />
              <Text style={s.bulletText}>{item}</Text>
            </View>
          ))}

          <Text style={[s.cardTitle, { marginTop: 16 }]}>What is retained</Text>
          {[
            'Recognition messages sent/received (anonymised)',
            'Points ledger history (financial records)',
            'Approved and fulfilled reward orders',
          ].map((item) => (
            <View key={item} style={s.bulletRow}>
              <Ionicons name="information-circle" size={16} color="#64748b" />
              <Text style={[s.bulletText, { color: '#64748b' }]}>{item}</Text>
            </View>
          ))}
        </View>

        {/* Confirmation inputs */}
        <View style={s.card}>
          <Text style={s.inputLabel}>
            Type <Text style={{ fontWeight: '800', color: DANGER }}>{CONFIRM_PHRASE}</Text> to confirm
          </Text>
          <TextInput
            value={confirmText}
            onChangeText={(v) => { setConfirmText(v); setError(null); }}
            placeholder={CONFIRM_PHRASE}
            placeholderTextColor="#cbd5e1"
            autoCapitalize="characters"
            autoCorrect={false}
            style={[s.input, confirmed && s.inputValid]}
          />

          <Text style={[s.inputLabel, { marginTop: 16 }]}>Enter your password</Text>
          <TextInput
            value={password}
            onChangeText={(v) => { setPassword(v); setError(null); }}
            placeholder="Your current password"
            placeholderTextColor="#cbd5e1"
            secureTextEntry
            style={s.input}
          />
        </View>

        {error && (
          <View style={s.errorBox}>
            <Ionicons name="alert-circle" size={16} color={DANGER} />
            <Text style={s.errorText}>{error}</Text>
          </View>
        )}

        <TouchableOpacity
          onPress={handleDelete}
          disabled={loading}
          activeOpacity={0.8}
          style={[s.deleteBtn, (!confirmed || loading) && s.deleteBtnDisabled]}
        >
          {loading
            ? <ActivityIndicator color="#fff" />
            : (
              <>
                <Ionicons name="trash" size={18} color="#fff" />
                <Text style={s.deleteBtnText}>Delete My Account</Text>
              </>
            )
          }
        </TouchableOpacity>

        <TouchableOpacity onPress={() => router.back()} style={s.cancelBtn}>
          <Text style={s.cancelText}>Keep My Account</Text>
        </TouchableOpacity>

        <View style={{ height: 40 }} />
      </ScrollView>
    </SafeAreaView>
  );
}

const s = StyleSheet.create({
  safe:    { flex: 1, backgroundColor: DANGER },
  scroll:  { flex: 1, backgroundColor: '#fff' },
  content: { paddingHorizontal: 20, paddingTop: 24 },

  header: {
    backgroundColor: DANGER,
    paddingHorizontal: 20,
    paddingTop: 10,
    paddingBottom: 20,
    flexDirection: 'row',
    alignItems: 'center',
  },
  backBtn: {
    width: 36, height: 36, borderRadius: 10,
    backgroundColor: 'rgba(255,255,255,0.25)',
    alignItems: 'center', justifyContent: 'center',
    marginRight: 12,
  },
  headerTitle: { fontSize: 20, fontWeight: '700', color: '#fff' },

  warnBox: {
    borderRadius: 16,
    backgroundColor: '#fff1f2',
    borderWidth: 1.5,
    borderColor: '#fecdd3',
    padding: 20,
    alignItems: 'center',
    marginBottom: 20,
  },
  warnTitle: { fontSize: 18, fontWeight: '800', color: DANGER, marginBottom: 10 },
  warnBody:  { fontSize: 14, color: '#9f1239', lineHeight: 22, textAlign: 'center' },

  card: {
    borderRadius: 16,
    backgroundColor: '#fff',
    borderWidth: 1,
    borderColor: '#e2e8f0',
    padding: 18,
    marginBottom: 16,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.05,
    shadowRadius: 8,
    elevation: 2,
  },
  cardTitle: { fontSize: 13, fontWeight: '700', color: '#475569', textTransform: 'uppercase', letterSpacing: 0.6, marginBottom: 10 },

  bulletRow: { flexDirection: 'row', alignItems: 'flex-start', marginBottom: 8 },
  bulletText: { flex: 1, marginLeft: 8, fontSize: 14, color: '#1e293b', lineHeight: 20 },

  inputLabel: { fontSize: 13, fontWeight: '600', color: '#475569', marginBottom: 8 },
  input: {
    borderWidth: 1.5,
    borderColor: '#e2e8f0',
    borderRadius: 12,
    paddingHorizontal: 14,
    paddingVertical: 12,
    fontSize: 15,
    color: '#0f172a',
    backgroundColor: '#f8fafc',
  },
  inputValid: { borderColor: '#22c55e', backgroundColor: '#f0fdf4' },

  errorBox: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#fff1f2',
    borderRadius: 12,
    paddingHorizontal: 14,
    paddingVertical: 12,
    marginBottom: 16,
  },
  errorText: { flex: 1, marginLeft: 8, fontSize: 13, color: DANGER },

  deleteBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: DANGER,
    borderRadius: 14,
    paddingVertical: 16,
    marginBottom: 12,
  },
  deleteBtnDisabled: { opacity: 0.45 },
  deleteBtnText: { marginLeft: 8, fontSize: 16, fontWeight: '700', color: '#fff' },

  cancelBtn: { alignItems: 'center', paddingVertical: 14 },
  cancelText: { fontSize: 15, fontWeight: '600', color: '#64748b' },
});
