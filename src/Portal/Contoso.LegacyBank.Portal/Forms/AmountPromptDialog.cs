using System;
using System.Drawing;
using System.Windows.Forms;

namespace Contoso.LegacyBank.Portal.Forms
{
    /// <summary>Minimal modal prompt for a single decimal amount and optional description,
    /// built by hand (no designer/resx) like the rest of this deliberately old-school UI.</summary>
    public class AmountPromptDialog : Form
    {
        private readonly TextBox _amountBox;
        private readonly TextBox _descriptionBox;

        public decimal Amount { get; private set; }
        public string Description { get; private set; }

        public AmountPromptDialog(string title, string amountLabel)
        {
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(340, 150);

            var lblAmount = new Label { Text = amountLabel, Location = new Point(12, 15), AutoSize = true };
            _amountBox = new TextBox { Location = new Point(140, 12), Width = 180 };

            var lblDescription = new Label { Text = "Description:", Location = new Point(12, 45), AutoSize = true };
            _descriptionBox = new TextBox { Location = new Point(140, 42), Width = 180 };

            var btnOk = new Button { Text = "OK", Location = new Point(155, 90), Width = 80, DialogResult = DialogResult.OK };
            var btnCancel = new Button { Text = "Cancel", Location = new Point(240, 90), Width = 80, DialogResult = DialogResult.Cancel };

            btnOk.Click += (s, e) =>
            {
                if (!decimal.TryParse(_amountBox.Text, out var amount) || amount <= 0)
                {
                    MessageBox.Show(this, "Enter a positive amount.", "Invalid amount", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    DialogResult = DialogResult.None;
                    return;
                }
                Amount = amount;
                Description = _descriptionBox.Text;
            };

            Controls.AddRange(new Control[] { lblAmount, _amountBox, lblDescription, _descriptionBox, btnOk, btnCancel });
            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }
    }
}
