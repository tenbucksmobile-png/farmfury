/**
 * Hotel reward voucher email template.
 *
 * Returns a plain-HTML string — no JSX dependencies, works in any server context.
 * Sent via Resend when an admin approves a hotel-category redemption.
 */

export interface VoucherEmailData {
  employeeName:  string;
  hotelName:     string;
  rewardTitle:   string;
  rewardImageUrl: string | null;
  voucherCode:   string;   // redemption ID
  pointsUsed:    number;
  terms:         string | null;
  approvedAt:    string;   // ISO date string
}

export function buildVoucherEmail(data: VoucherEmailData): { subject: string; html: string } {
  const subject = `Your ${data.hotelName} Reward Voucher — ${data.rewardTitle}`;

  const imageBlock = data.rewardImageUrl
    ? `<img src="${data.rewardImageUrl}" alt="${escHtml(data.rewardTitle)}"
            style="width:100%;max-width:480px;height:220px;object-fit:cover;
                   border-radius:12px;display:block;margin:0 auto 24px;" />`
    : '';

  const termsBlock = data.terms
    ? `<div style="margin-top:28px;padding:16px;background:#f5f3ff;border-radius:10px;
                  border-left:4px solid #7B1FA2;">
         <p style="margin:0 0 6px;font-size:12px;font-weight:700;color:#7B1FA2;
                   text-transform:uppercase;letter-spacing:0.5px;">Terms &amp; Conditions</p>
         <p style="margin:0;font-size:13px;color:#374151;line-height:1.6;">${escHtml(data.terms)}</p>
       </div>`
    : '';

  const html = `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <title>${escHtml(subject)}</title>
</head>
<body style="margin:0;padding:0;background:#f5f3ff;font-family:'Helvetica Neue',Helvetica,Arial,sans-serif;">

  <!-- Wrapper -->
  <table width="100%" cellpadding="0" cellspacing="0" style="background:#f5f3ff;padding:32px 16px;">
    <tr>
      <td align="center">
        <table width="100%" style="max-width:560px;background:#ffffff;border-radius:20px;
                                    overflow:hidden;box-shadow:0 4px 24px rgba(0,0,0,0.08);">

          <!-- Header -->
          <tr>
            <td style="background:#7B1FA2;padding:28px 32px;text-align:center;">
              <p style="margin:0;font-size:11px;font-weight:800;color:rgba(255,255,255,0.7);
                         letter-spacing:2px;text-transform:uppercase;">
                ${escHtml(data.hotelName)}
              </p>
              <h1 style="margin:8px 0 0;font-size:22px;font-weight:800;color:#ffffff;">
                Indaba Cares
              </h1>
              <p style="margin:6px 0 0;font-size:13px;color:rgba(255,255,255,0.8);">
                Your reward voucher is ready
              </p>
            </td>
          </tr>

          <!-- Body -->
          <tr>
            <td style="padding:32px;">

              <p style="margin:0 0 24px;font-size:15px;color:#374151;line-height:1.6;">
                Hi <strong>${escHtml(data.employeeName)}</strong>,<br />
                Great news! Your reward redemption has been approved. Here is your voucher.
              </p>

              ${imageBlock}

              <!-- Reward name -->
              <h2 style="margin:0 0 8px;font-size:20px;font-weight:800;color:#1e1b4b;">
                ${escHtml(data.rewardTitle)}
              </h2>
              <p style="margin:0 0 24px;font-size:13px;color:#6b7280;">
                ${data.pointsUsed} points redeemed &nbsp;·&nbsp; Approved ${formatDate(data.approvedAt)}
              </p>

              <!-- Voucher code box -->
              <div style="background:#f5f3ff;border:2px dashed #7B1FA2;border-radius:14px;
                           padding:20px;text-align:center;margin-bottom:8px;">
                <p style="margin:0 0 6px;font-size:11px;font-weight:700;color:#7B1FA2;
                           text-transform:uppercase;letter-spacing:1px;">Voucher Code</p>
                <p style="margin:0;font-size:26px;font-weight:900;color:#1e1b4b;
                           letter-spacing:3px;font-family:'Courier New',monospace;">
                  ${data.voucherCode.toUpperCase().replace(/-/g, '&nbsp;-&nbsp;')}
                </p>
              </div>
              <p style="margin:0 0 24px;font-size:11px;color:#9ca3af;text-align:center;">
                Present this code at ${escHtml(data.hotelName)} to redeem your reward.
              </p>

              ${termsBlock}

            </td>
          </tr>

          <!-- Footer -->
          <tr>
            <td style="background:#faf5ff;padding:20px 32px;border-top:1px solid #ede9fe;
                        text-align:center;">
              <p style="margin:0;font-size:11px;color:#9ca3af;line-height:1.6;">
                This voucher was issued by <strong>${escHtml(data.hotelName)}</strong> via Indaba Cares.<br />
                If you have questions, please contact your hotel HR department.
              </p>
            </td>
          </tr>

        </table>
      </td>
    </tr>
  </table>

</body>
</html>`;

  return { subject, html };
}

function escHtml(str: string): string {
  return str
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

function formatDate(iso: string): string {
  try {
    return new Date(iso).toLocaleDateString('en-ZA', {
      day: 'numeric', month: 'long', year: 'numeric',
    });
  } catch {
    return iso;
  }
}
