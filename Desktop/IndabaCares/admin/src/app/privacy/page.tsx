import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Privacy Policy — IndabaCares',
  description: 'Privacy policy for the IndabaCares employee recognition and engagement app.',
};

export default function PrivacyPage() {
  return (
    <div className="min-h-screen bg-white px-6 py-12">
      <div className="mx-auto max-w-2xl text-sm text-gray-700 leading-relaxed">

        <h1 className="text-2xl font-bold text-gray-900 mb-1">Privacy Policy</h1>
        <p className="text-gray-400 text-xs mb-8">Last updated: June 2025</p>

        <p className="mb-6">
          IndabaCares is a private employee recognition and engagement platform operated by Indaba
          Hotel Group. The app is available exclusively to verified employees and is not open to
          the general public. This policy explains what personal data we collect, how we use it,
          and your rights.
        </p>

        <Section title="1. Data We Collect">
          <ul className="list-disc pl-5 space-y-1">
            <li><strong>Identity:</strong> Full name, employee code, hotel, department, and job title — provided at account creation.</li>
            <li><strong>Profile photo:</strong> Optional image uploaded by the employee.</li>
            <li><strong>Recognition activity:</strong> Peer recognition messages sent and received, reactions, and comments.</li>
            <li><strong>Mood check-ins:</strong> Daily mood ratings (Awful / Bad / Okay / Good / Amazing).</li>
            <li><strong>Reward activity:</strong> Points balance, wallet balance, and redemption history.</li>
            <li><strong>Chat messages:</strong> Messages sent within in-app team chat.</li>
            <li><strong>Push notification token:</strong> Device token used to deliver push notifications.</li>
            <li><strong>Session token:</strong> A secure UUID stored in the device keychain to authenticate your session.</li>
          </ul>
        </Section>

        <Section title="2. How We Use Your Data">
          <ul className="list-disc pl-5 space-y-1">
            <li>To operate peer recognition, rewards, leaderboards, mood tracking, and chat features.</li>
            <li>To send push notifications for recognitions, reactions, and reward updates.</li>
            <li>To provide hotel management with engagement analytics via the admin dashboard.</li>
            <li>To send voucher emails when a reward redemption is approved.</li>
          </ul>
        </Section>

        <Section title="3. Data Sharing">
          <p className="mb-2">We do not sell or share your personal data with advertisers or unaffiliated third parties. Data is processed by the following service providers solely to operate the platform:</p>
          <ul className="list-disc pl-5 space-y-1">
            <li><strong>Supabase</strong> — cloud database and file storage (EU region).</li>
            <li><strong>Expo / Apple Push Notification Service</strong> — delivery of push notifications to iOS devices.</li>
            <li><strong>Resend</strong> — transactional email delivery for reward vouchers.</li>
          </ul>
        </Section>

        <Section title="4. Data Security">
          <ul className="list-disc pl-5 space-y-1">
            <li>All data is transmitted over HTTPS. Plain-HTTP connections are rejected by the app.</li>
            <li>Session tokens are stored in the iOS Keychain (encrypted at rest on-device).</li>
            <li>Each employee's data is isolated to their hotel by row-level security policies — employees at one hotel cannot access data from another hotel.</li>
            <li>The admin dashboard is accessible only to authorised hotel administrators.</li>
          </ul>
        </Section>

        <Section title="5. Data Retention">
          <p>
            Your data is retained for as long as you are an active employee. When your account is
            deactivated, your session is revoked immediately. You may request deletion of your
            personal data by contacting your HR administrator or emailing us directly.
          </p>
        </Section>

        <Section title="6. Your Rights">
          <p className="mb-2">You have the right to:</p>
          <ul className="list-disc pl-5 space-y-1">
            <li>Access the personal data we hold about you.</li>
            <li>Request correction of inaccurate information.</li>
            <li>Request deletion of your account and associated data.</li>
          </ul>
          <p className="mt-2">To exercise these rights, contact your HR department or email us at the address below.</p>
        </Section>

        <Section title="7. Children">
          <p>
            IndabaCares is intended for use by adults in an employment context. It is not directed
            at children under the age of 13.
          </p>
        </Section>

        <Section title="8. Changes to This Policy">
          <p>
            We may update this policy from time to time. The date at the top of this page reflects
            the most recent revision. Continued use of the app after changes constitutes acceptance
            of the updated policy.
          </p>
        </Section>

        <Section title="9. Contact">
          <p>
            For privacy-related questions or data requests, contact:
            <br />
            <strong>Indaba Hotel Group — Human Resources</strong>
            <br />
            <a href="mailto:hr@indabahotel.co.za" className="text-violet-600 underline">
              hr@indabahotel.co.za
            </a>
          </p>
        </Section>

      </div>
    </div>
  );
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="mb-6">
      <h2 className="text-base font-semibold text-gray-900 mb-2">{title}</h2>
      {children}
    </div>
  );
}
