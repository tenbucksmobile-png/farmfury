import React from 'react';
import { View, Text, ScrollView, StyleSheet, Linking, TouchableOpacity } from 'react-native';
import { Stack, router } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { SafeAreaView } from 'react-native-safe-area-context';

const PURPLE = '#7C3AED';
const CONTACT_EMAIL = 'privacy@indabacares.com';

export default function PrivacyPolicyScreen() {
  return (
    <SafeAreaView style={s.safe} edges={['top']}>
      <Stack.Screen options={{ headerShown: false }} />

      <View style={s.header}>
        <TouchableOpacity onPress={() => router.back()} style={s.backBtn} hitSlop={12}>
          <Ionicons name="chevron-back" size={22} color="#fff" />
        </TouchableOpacity>
        <Text style={s.headerTitle}>Privacy Policy</Text>
      </View>

      <ScrollView style={s.scroll} contentContainerStyle={s.content} showsVerticalScrollIndicator={false}>

        <Text style={s.docTitle}>Indaba Hospitality Group{'\n'}Employee Rewards Application</Text>

        <Section title="1. INTRODUCTION">
          <Body>
            This Privacy Policy explains how personal information is collected, processed, and protected in accordance with:{'\n'}
            • Protection of Personal Information Act, 4 of 2013 (POPIA){'\n'}
            • Electronic Communications and Transactions Act, 25 of 2002 (ECTA){'\n\n'}
            The App is:{'\n'}
            • Owned by Indaba Hospitality Group{'\n'}
            • Managed by Tenbucks-Mobile (PTY) Ltd
          </Body>
        </Section>

        <Section title="2. INFORMATION WE COLLECT">
          <SubHeading>2.1 Personal Information</SubHeading>
          <Body>
            We may collect:{'\n'}
            • Full name{'\n'}
            • Employee ID{'\n'}
            • Contact details (email, phone){'\n'}
            • Job role and department{'\n'}
            • Employment status
          </Body>
          <SubHeading>2.2 Usage Data</SubHeading>
          <Body>
            • App activity{'\n'}
            • Reward interactions{'\n'}
            • Login history{'\n'}
            • Device information
          </Body>
          <SubHeading>2.3 Technical Data</SubHeading>
          <Body>
            • IP address{'\n'}
            • Device type{'\n'}
            • Operating system
          </Body>
        </Section>

        <Section title="3. PURPOSE OF PROCESSING">
          <Body>
            Your information is processed for:{'\n'}
            • Employee verification{'\n'}
            • Reward allocation{'\n'}
            • Performance recognition tracking{'\n'}
            • Internal reporting{'\n'}
            • App functionality and security
          </Body>
        </Section>

        <Section title="4. LEGAL BASIS FOR PROCESSING (POPIA)">
          <Body>
            Processing is justified under:{'\n'}
            • Employment relationship necessity{'\n'}
            • Legitimate business interests{'\n'}
            • Consent (where applicable){'\n'}
            • Legal obligations
          </Body>
        </Section>

        <Section title="5. DATA SHARING">
          <Body>
            Your data may be shared with:{'\n'}
            • Indaba Hospitality Group internal departments{'\n'}
            • Tenbucks-Mobile (PTY) Ltd (system management){'\n'}
            • Third-party service providers (where necessary){'\n\n'}
            We do not sell personal data.
          </Body>
        </Section>

        <Section title="6. DATA SECURITY">
          <Body>
            We implement:{'\n'}
            • Encryption protocols{'\n'}
            • Secure authentication systems{'\n'}
            • Access controls{'\n'}
            • Regular system monitoring{'\n\n'}
            However, no system is completely secure.
          </Body>
        </Section>

        <Section title="7. DATA RETENTION">
          <Body>
            We retain data:{'\n'}
            • For the duration of employment{'\n'}
            • As required by law{'\n'}
            • As necessary for operational purposes{'\n\n'}
            Data is deleted or anonymized when no longer required.
          </Body>
        </Section>

        <Section title="8. USER RIGHTS (POPIA)">
          <Body>
            You have the right to:{'\n'}
            • Access your personal data{'\n'}
            • Request correction{'\n'}
            • Request deletion (where applicable){'\n'}
            • Object to processing{'\n'}
            • Lodge a complaint with the Information Regulator
          </Body>
        </Section>

        <Section title="9. CROSS-BORDER DATA TRANSFERS">
          <Body>
            If data is transferred outside South Africa:{'\n'}
            • It will be protected under equivalent safeguards{'\n'}
            • Compliant jurisdictions or agreements will be used
          </Body>
        </Section>

        <Section title="10. COOKIES AND TRACKING">
          <Body>
            The App may use session tracking and analytics tools, strictly for performance and user experience improvements.
          </Body>
        </Section>

        <Section title="11. CHILDREN'S PRIVACY">
          <Body>The App is not intended for individuals under 18.</Body>
        </Section>

        <Section title="12. BREACH NOTIFICATION">
          <Body>
            In the event of a data breach:{'\n'}
            • Users will be notified where required{'\n'}
            • Authorities will be informed in line with POPIA
          </Body>
        </Section>

        <Section title="13. CHANGES TO POLICY">
          <Body>
            We may update this policy periodically. Users will be notified via app notifications or internal communication channels.
          </Body>
        </Section>

        <Section title="14. CONTACT DETAILS">
          <Body>For all privacy-related queries:</Body>
          <Body>Tenbucks-Mobile (PTY) Ltd</Body>
          <TouchableOpacity onPress={() => Linking.openURL(`mailto:${CONTACT_EMAIL}`)}>
            <Text style={s.link}>{CONTACT_EMAIL}</Text>
          </TouchableOpacity>
        </Section>

        <Section title="15. INFORMATION REGULATOR (SOUTH AFRICA)">
          <Body>
            If unresolved, complaints may be directed to the Information Regulator (South Africa).
          </Body>
        </Section>

        <View style={{ height: 40 }} />
      </ScrollView>
    </SafeAreaView>
  );
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <View style={s.section}>
      <Text style={s.sectionTitle}>{title}</Text>
      {children}
    </View>
  );
}

function SubHeading({ children }: { children: string }) {
  return <Text style={s.subHeading}>{children}</Text>;
}

function Body({ children }: { children: React.ReactNode }) {
  return <Text style={s.body}>{children}</Text>;
}

const s = StyleSheet.create({
  safe:   { flex: 1, backgroundColor: PURPLE },
  scroll: { flex: 1, backgroundColor: '#fff' },
  content: { paddingHorizontal: 20, paddingTop: 24, paddingBottom: 48 },

  header: {
    backgroundColor: PURPLE,
    paddingHorizontal: 20,
    paddingTop: 10,
    paddingBottom: 20,
    flexDirection: 'row',
    alignItems: 'center',
  },
  backBtn: {
    width: 36, height: 36, borderRadius: 10,
    backgroundColor: 'rgba(255,255,255,0.18)',
    alignItems: 'center', justifyContent: 'center',
    marginRight: 12,
  },
  headerTitle: { fontSize: 20, fontWeight: '700', color: '#fff' },

  docTitle: {
    fontSize: 14,
    fontWeight: '700',
    color: '#1e1b4b',
    textAlign: 'center',
    marginBottom: 24,
    lineHeight: 22,
  },

  section:      { marginBottom: 24 },
  sectionTitle: { fontSize: 14, fontWeight: '700', color: '#4c1d95', marginBottom: 8 },
  subHeading:   { fontSize: 13, fontWeight: '600', color: '#6d28d9', marginTop: 8, marginBottom: 4 },
  body:         { fontSize: 13, color: '#374151', lineHeight: 21, marginBottom: 6 },
  link:         { fontSize: 13, color: PURPLE, textDecorationLine: 'underline', marginTop: 4 },
});
