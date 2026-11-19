using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Contoso.LegacyBank.Accounts.Contracts;
using Contoso.LegacyBank.Loans.Contracts;

namespace Contoso.LegacyBank.Portal.Forms
{
    public class NewLoanDialog : Form
    {
        private readonly TextBox _customerNumberBox;
        private readonly ComboBox _productCombo;
        private readonly TextBox _amountBox;
        private readonly TextBox _termBox;
        private readonly IAccountService _accountService;
        private readonly LoanProductDto[] _products;

        public int CustomerId { get; private set; }
        public int LoanProductId { get; private set; }
        public decimal PrincipalAmount { get; private set; }
        public int TermMonths { get; private set; }

        public NewLoanDialog(IAccountService accountService, LoanProductDto[] products)
        {
            _accountService = accountService;
            _products = products;

            Text = "Originate New Loan";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(380, 210);

            var lblCustomer = new Label { Text = "Customer number:", Location = new Point(12, 15), AutoSize = true };
            _customerNumberBox = new TextBox { Location = new Point(160, 12), Width = 190 };

            var lblProduct = new Label { Text = "Loan product:", Location = new Point(12, 45), AutoSize = true };
            _productCombo = new ComboBox { Location = new Point(160, 42), Width = 190, DropDownStyle = ComboBoxStyle.DropDownList };
            foreach (var product in products)
                _productCombo.Items.Add($"{product.ProductCode} - {product.Name} ({product.AnnualInterestRate:P2})");
            if (products.Length > 0) _productCombo.SelectedIndex = 0;

            var lblAmount = new Label { Text = "Principal amount:", Location = new Point(12, 75), AutoSize = true };
            _amountBox = new TextBox { Location = new Point(160, 72), Width = 190 };

            var lblTerm = new Label { Text = "Term (months):", Location = new Point(12, 105), AutoSize = true };
            _termBox = new TextBox { Location = new Point(160, 102), Width = 190 };

            var btnOk = new Button { Text = "Originate", Location = new Point(190, 145), Width = 90, DialogResult = DialogResult.OK };
            var btnCancel = new Button { Text = "Cancel", Location = new Point(285, 145), Width = 80, DialogResult = DialogResult.Cancel };

            btnOk.Click += OnOk;

            Controls.AddRange(new Control[]
            {
                lblCustomer, _customerNumberBox, lblProduct, _productCombo,
                lblAmount, _amountBox, lblTerm, _termBox, btnOk, btnCancel
            });
            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }

        private void OnOk(object sender, EventArgs e)
        {
            if (_productCombo.SelectedIndex < 0)
            {
                Fail("Select a loan product.");
                return;
            }
            if (!decimal.TryParse(_amountBox.Text, out var amount) || amount <= 0)
            {
                Fail("Enter a positive principal amount.");
                return;
            }
            if (!int.TryParse(_termBox.Text, out var term) || term <= 0)
            {
                Fail("Enter a positive term in months.");
                return;
            }

            try
            {
                var account = _accountService.SearchAccounts(_customerNumberBox.Text.Trim()).FirstOrDefault();
                if (account == null)
                {
                    Fail("No customer/account found for that customer number.");
                    return;
                }

                CustomerId = account.Customer.CustomerId;
                LoanProductId = _products[_productCombo.SelectedIndex].LoanProductId;
                PrincipalAmount = amount;
                TermMonths = term;
            }
            catch (Exception ex)
            {
                Fail("Could not resolve customer: " + ex.Message);
            }
        }

        private void Fail(string message)
        {
            MessageBox.Show(this, message, "Invalid input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
        }
    }
}
