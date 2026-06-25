import React from 'react';
import { View, Text, ScrollView, StyleSheet, TouchableOpacity } from 'react-native';
import { Stack, router } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { SafeAreaView } from 'react-native-safe-area-context';

const PURPLE = '#7C3AED';

export default function TermsOfServiceScreen() {
  return (
    <SafeAreaView style={s.safe} edges={['top']}>
      <Stack.Screen options={{ headerShown: false }} />

      <View style={s.header}>
        <TouchableOpacity onPress={() => router.back()} style={s.backBtn} hitSlop={12}>
          <Ionicons name="chevron-back" size={22} color="#fff" />
        </TouchableOpacity>
        <Text style={s.headerTitle}>Terms of Use</Text>
      </View>

      <ScrollView style={s.scroll} contentContainerStyle={s.content} showsVerticalScrollIndicator={false}>

        <Text style={s.docTitle}>Indaba Hospitality Group{'\n'}Employee Rewards & Recognition Application</Text>

        <Section title="1. INTRODUCTION">
          <Body>
            These Terms of Use ("Terms") govern access to and use of the Indaba Hospitality Group Employee Rewards & Recognition Application ("the App").{'\n\n'}
            The App is:{'\n'}
            • Owned by Indaba Hospitality Group ("the Company"){'\n'}
            • Managed and operated by Tenbucks-Mobile (PTY) Ltd ("Managing Agent"){'\n'}
            • Restricted exclusively to verified employees of Indaba Hospitality Group and its subsidiaries{'\n\n'}
            By accessing or using the App, you agree to be bound by these Terms. If you do not agree, you must immediately discontinue use.
          </Body>
        </Section>

        <Section title="2. ELIGIBILITY AND ACCESS CONTROL">
          <SubHeading>2.1 Restricted Access</SubHeading>
          <Body>
            The App is a closed system, accessible only to:{'\n'}
            • Current employees of Indaba Hospitality Group{'\n'}
            • Employees of its subsidiaries and affiliated entities
          </Body>
          <SubHeading>2.2 Verification</SubHeading>
          <Body>
            Access is granted based on employee records, internal authentication systems, and employer-issued credentials. The Company reserves the right to approve or deny access, and revoke access at any time.
          </Body>
          <SubHeading>2.3 Termination of Access</SubHeading>
          <Body>
            Access will be terminated if employment ends, misuse is detected, or policy violations occur.
          </Body>
        </Section>

        <Section title="3. PURPOSE OF THE APPLICATION">
          <Body>
            The App provides:{'\n'}
            • Employee reward allocation{'\n'}
            • Recognition programs{'\n'}
            • Incentive tracking{'\n'}
            • Internal engagement features{'\n\n'}
            The App is strictly for internal corporate use.
          </Body>
        </Section>

        <Section title="4. USER RESPONSIBILITIES">
          <Body>
            You agree to:{'\n'}
            • Use the App lawfully and in good faith{'\n'}
            • Maintain confidentiality of login credentials{'\n'}
            • Not share access with unauthorized users{'\n'}
            • Not manipulate or exploit reward systems{'\n\n'}
            You may not:{'\n'}
            • Reverse engineer the App{'\n'}
            • Attempt unauthorized access{'\n'}
            • Interfere with system security{'\n'}
            • Use the App for fraudulent purposes
          </Body>
        </Section>

        <Section title="5. REWARDS AND INCENTIVES">
          <SubHeading>5.1 Nature of Rewards</SubHeading>
          <Body>
            Rewards are discretionary, have no cash equivalence unless explicitly stated, and are subject to internal policies.
          </Body>
          <SubHeading>5.2 Modification</SubHeading>
          <Body>
            The Company reserves the right to change reward structures, adjust point allocations, and withdraw or replace incentives.
          </Body>
          <SubHeading>5.3 No Guarantee</SubHeading>
          <Body>
            Participation does not guarantee rewards or continued availability of programs.
          </Body>
        </Section>

        <Section title="6. INTELLECTUAL PROPERTY">
          <Body>
            All content within the App — including software, branding, design, and data — is owned by Indaba Hospitality Group and/or Tenbucks-Mobile (PTY) Ltd. Unauthorized use is strictly prohibited.
          </Body>
        </Section>

        <Section title="7. DATA USAGE AND PRIVACY">
          <Body>
            Use of the App is subject to the Privacy Policy, in compliance with the Protection of Personal Information Act (POPIA) and the Electronic Communications and Transactions Act (ECTA).
          </Body>
        </Section>

        <Section title="8. DISCLAIMERS">
          <Body>
            The App is provided "as is" with no warranties of uninterrupted service, no guarantee of accuracy of all content, and is subject to system downtime and maintenance.
          </Body>
        </Section>

        <Section title="9. LIMITATION OF LIABILITY">
          <Body>
            To the fullest extent permitted by law, the Company and Managing Agent are not liable for indirect or consequential damages, loss of rewards due to technical issues, or unauthorized access due to user negligence.
          </Body>
        </Section>

        <Section title="10. TERMINATION">
          <Body>
            The Company may suspend or terminate access without prior notice, for operational, legal, or disciplinary reasons.
          </Body>
        </Section>

        <Section title="11. GOVERNING LAW">
          <Body>
            These Terms are governed by the laws of the Republic of South Africa. Disputes will be subject to South African courts.
          </Body>
        </Section>

        <Section title="12. CONTACT DETAILS">
          <Body>
            Managing Agent:{'\n'}
            Tenbucks-Mobile (PTY) Ltd{'\n'}
            Email: support@tenbucks-mobile.co.za
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
  safe:    { flex: 1, backgroundColor: PURPLE },
  scroll:  { flex: 1, backgroundColor: '#fff' },
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
});
