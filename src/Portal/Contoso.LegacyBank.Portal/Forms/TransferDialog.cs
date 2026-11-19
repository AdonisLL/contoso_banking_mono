using System.Drawing;
using System.Windows.Forms;

namespace Contoso.LegacyBank.Portal.Forms
{
    public class TransferDialog : Form
    {
        private readonly TextBox _toAccountBox;
        private readonly TextBox _amountBox;
        private readonly TextBox _descriptionBox;

        public string ToAccountNumber { get; private set; }
        public decimal Amount { get; private set; }
        public string Description { get; private set; }

        public TransferDialog()
        {
            Text = "Transfer Funds";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(360, 180);

            var lblTo = new Label { Text = "To account number:", Location = new Point(12, 15), AutoSize = true };
            _toAccountBox = new TextBox { Location = new Point(160, 12), Width = 180 };

            var lblAmount = new Label { Text = "Amount:", Location = new Point(12, 45), AutoSize = true };
            _amountBox = new TextBox { Location = new Point(160, 42), Width = 180 };

            var lblDescription = new Label { Text = "Description:", Location = new Point(12, 75), AutoSize = true };
            _descriptionBox = new TextBox { Location = new Point(160, 72), Width = 180 };

            var btnOk = new Button { Text = "Transfer", Location = new Point(175, 120), Width = 90, DialogResult = DialogResult.OK };
            var btnCancel = new Button { Text = "Cancel", Location = new Point(270, 120), Width = 80, DialogResult = DialogResult.Cancel };

            btnOk.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_toAccountBox.Text))
                {
                    MessageBox.Show(this, "Enter a destination account number.", "Invalid input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    DialogResult = DialogResult.None;
                    return;
                }
                if (!decimal.TryParse(_amountBox.Text, out var amount) || amount <= 0)
                {
                    MessageBox.Show(this, "Enter a positive amount.", "Invalid amount", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    DialogResult = DialogResult.None;
                    return;
                }
                ToAccountNumber = _toAccountBox.Text.Trim();
                Amount = amount;
                Description = _descriptionBox.Text;
            };

            Controls.AddRange(new Control[] { lblTo, _toAccountBox, lblAmount, _amountBox, lblDescription, _descriptionBox, btnOk, btnCancel });
            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }
    }
}
