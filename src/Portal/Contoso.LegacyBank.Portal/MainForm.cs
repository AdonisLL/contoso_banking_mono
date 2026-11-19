using System;
using System.Diagnostics;
using System.Linq;
using System.ServiceModel;
using System.Windows.Forms;
using Contoso.LegacyBank.Accounts.Contracts;
using Contoso.LegacyBank.Loans.Contracts;
using Contoso.LegacyBank.Portal.Forms;
using Contoso.LegacyBank.Portal.Models;
using Contoso.LegacyBank.Portal.Services;

namespace Contoso.LegacyBank.Portal
{
    /// <summary>
    /// Teller-style main window: customer/account search and transactions, loan origination
    /// and payments, statement requests, and a read-only notifications feed. Built entirely in
    /// code (no designer/resx) - the UI is intentionally plain, synchronous-feeling, and
    /// un-fancy, matching the rest of this legacy estate.
    /// </summary>
    public class MainForm : Form
    {
        private DataGridView _accountsGrid;
        private DataGridView _transactionsGrid;
        private TextBox _accountSearchBox;
        private Button _depositButton;
        private Button _withdrawButton;
        private Button _transferButton;
        private AccountDto _selectedAccount;

        private DataGridView _loansGrid;
        private DataGridView _scheduleGrid;
        private TextBox _loanSearchBox;
        private Button _applyPaymentButton;
        private LoanDto _selectedLoan;

        private TextBox _statementAccountBox;
        private DateTimePicker _statementStartPicker;
        private DateTimePicker _statementEndPicker;
        private Label _statementResultLabel;
        private TextBox _statementIdBox;
        private Button _openPdfButton;
        private string _lastStatementFilePath;

        private DataGridView _notificationsGrid;

        public MainForm()
        {
            Text = "Contoso Legacy Bank - Teller Portal";
            Width = 1000;
            Height = 700;
            StartPosition = FormStartPosition.CenterScreen;

            var tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(BuildAccountsTab());
            tabs.TabPages.Add(BuildLoansTab());
            tabs.TabPages.Add(BuildStatementsTab());
            tabs.TabPages.Add(BuildNotificationsTab());
            Controls.Add(tabs);
        }

        #region Accounts tab

        private TabPage BuildAccountsTab()
        {
            var page = new TabPage("Accounts");

            _accountSearchBox = new TextBox { Location = new System.Drawing.Point(10, 10), Width = 250 };
            var searchButton = new Button { Text = "Search", Location = new System.Drawing.Point(270, 8), Width = 90 };
            searchButton.Click += (s, e) => SearchAccounts();

            _accountsGrid = new DataGridView
            {
                Location = new System.Drawing.Point(10, 45),
                Size = new System.Drawing.Size(960, 220),
                ReadOnly = true,
                AutoGenerateColumns = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AllowUserToAddRows = false
            };
            _accountsGrid.SelectionChanged += (s, e) => LoadSelectedAccount();

            _transactionsGrid = new DataGridView
            {
                Location = new System.Drawing.Point(10, 310),
                Size = new System.Drawing.Size(960, 220),
                ReadOnly = true,
                AutoGenerateColumns = true,
                AllowUserToAddRows = false
            };

            _depositButton = new Button { Text = "Deposit", Location = new System.Drawing.Point(10, 540), Width = 100, Enabled = false };
            _depositButton.Click += (s, e) => DepositOrWithdraw(isDeposit: true);

            _withdrawButton = new Button { Text = "Withdraw", Location = new System.Drawing.Point(120, 540), Width = 100, Enabled = false };
            _withdrawButton.Click += (s, e) => DepositOrWithdraw(isDeposit: false);

            _transferButton = new Button { Text = "Transfer...", Location = new System.Drawing.Point(230, 540), Width = 100, Enabled = false };
            _transferButton.Click += (s, e) => TransferFunds();

            page.Controls.AddRange(new Control[]
            {
                _accountSearchBox, searchButton, _accountsGrid, _transactionsGrid,
                _depositButton, _withdrawButton, _transferButton
            });
            return page;
        }

        private void SearchAccounts()
        {
            try
            {
                var client = ServiceClients.CreateAccountClient();
                var accounts = client.SearchAccounts(_accountSearchBox.Text.Trim());
                _accountsGrid.DataSource = accounts.Select(a => new AccountRow
                {
                    AccountId = a.AccountId,
                    AccountNumber = a.AccountNumber,
                    CustomerName = $"{a.Customer.FirstName} {a.Customer.LastName}",
                    CustomerNumber = a.Customer.CustomerNumber,
                    AccountType = a.AccountType,
                    Status = a.Status,
                    Balance = a.Balance,
                    OverdraftLimit = a.OverdraftLimit
                }).ToList();
            }
            catch (Exception ex)
            {
                ShowError("Account search failed", ex);
            }
        }

        private void LoadSelectedAccount()
        {
            if (!(_accountsGrid.CurrentRow?.DataBoundItem is AccountRow row))
            {
                _selectedAccount = null;
                _depositButton.Enabled = _withdrawButton.Enabled = _transferButton.Enabled = false;
                return;
            }

            try
            {
                var client = ServiceClients.CreateAccountClient();
                _selectedAccount = client.GetAccountById(row.AccountId);
                var history = client.GetTransactionHistory(row.AccountId, null, null);
                _transactionsGrid.DataSource = history.Select(t => new TransactionRow
                {
                    TransactionId = t.TransactionId,
                    CreatedUtc = t.CreatedUtc,
                    TransactionType = t.TransactionType,
                    Amount = t.Amount,
                    BalanceAfter = t.BalanceAfter,
                    Description = t.Description
                }).ToList();

                _depositButton.Enabled = _withdrawButton.Enabled = _transferButton.Enabled = true;
            }
            catch (Exception ex)
            {
                ShowError("Could not load account details", ex);
            }
        }

        private void DepositOrWithdraw(bool isDeposit)
        {
            if (_selectedAccount == null) return;

            using (var dialog = new AmountPromptDialog(isDeposit ? "Deposit" : "Withdraw", "Amount:"))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    var client = ServiceClients.CreateAccountClient();
                    if (isDeposit)
                        client.PostDeposit(_selectedAccount.AccountId, dialog.Amount, dialog.Description, null);
                    else
                        client.PostWithdrawal(_selectedAccount.AccountId, dialog.Amount, dialog.Description, null);

                    LoadSelectedAccount();
                    SearchAccounts();
                }
                catch (FaultException<AccountFault> ex)
                {
                    MessageBox.Show(this, ex.Detail.Message, ex.Detail.Code, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                catch (Exception ex)
                {
                    ShowError("Transaction failed", ex);
                }
            }
        }

        private void TransferFunds()
        {
            if (_selectedAccount == null) return;

            using (var dialog = new TransferDialog())
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    var client = ServiceClients.CreateAccountClient();
                    var toAccount = client.GetAccountByNumber(dialog.ToAccountNumber);
                    client.TransferFunds(_selectedAccount.AccountId, toAccount.AccountId, dialog.Amount, dialog.Description);

                    LoadSelectedAccount();
                    SearchAccounts();
                    MessageBox.Show(this, "Transfer completed.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (FaultException<AccountFault> ex)
                {
                    MessageBox.Show(this, ex.Detail.Message, ex.Detail.Code, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                catch (Exception ex)
                {
                    ShowError("Transfer failed", ex);
                }
            }
        }

        #endregion

        #region Loans tab

        private TabPage BuildLoansTab()
        {
            var page = new TabPage("Loans");

            _loanSearchBox = new TextBox { Location = new System.Drawing.Point(10, 10), Width = 250 };
            var searchButton = new Button { Text = "Search", Location = new System.Drawing.Point(270, 8), Width = 90 };
            searchButton.Click += (s, e) => SearchLoans();

            var newLoanButton = new Button { Text = "New Loan...", Location = new System.Drawing.Point(370, 8), Width = 100 };
            newLoanButton.Click += (s, e) => OriginateLoan();

            _loansGrid = new DataGridView
            {
                Location = new System.Drawing.Point(10, 45),
                Size = new System.Drawing.Size(960, 220),
                ReadOnly = true,
                AutoGenerateColumns = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AllowUserToAddRows = false
            };
            _loansGrid.SelectionChanged += (s, e) => LoadSelectedLoan();

            _scheduleGrid = new DataGridView
            {
                Location = new System.Drawing.Point(10, 310),
                Size = new System.Drawing.Size(960, 220),
                ReadOnly = true,
                AutoGenerateColumns = true,
                AllowUserToAddRows = false
            };

            _applyPaymentButton = new Button { Text = "Apply Payment...", Location = new System.Drawing.Point(10, 540), Width = 130, Enabled = false };
            _applyPaymentButton.Click += (s, e) => ApplyLoanPayment();

            page.Controls.AddRange(new Control[]
            {
                _loanSearchBox, searchButton, newLoanButton, _loansGrid, _scheduleGrid, _applyPaymentButton
            });
            return page;
        }

        private void SearchLoans()
        {
            try
            {
                var client = ServiceClients.CreateLoanClient();
                var loans = client.SearchLoans(_loanSearchBox.Text.Trim());
                _loansGrid.DataSource = loans.Select(l => new LoanRow
                {
                    LoanId = l.LoanId,
                    LoanNumber = l.LoanNumber,
                    CustomerName = $"{l.FirstName} {l.LastName}",
                    ProductName = l.ProductName,
                    PrincipalAmount = l.PrincipalAmount,
                    OutstandingPrincipal = l.OutstandingPrincipal,
                    Status = l.Status,
                    MaturityDate = l.MaturityDate
                }).ToList();
            }
            catch (Exception ex)
            {
                ShowError("Loan search failed", ex);
            }
        }

        private void LoadSelectedLoan()
        {
            if (!(_loansGrid.CurrentRow?.DataBoundItem is LoanRow row))
            {
                _selectedLoan = null;
                _applyPaymentButton.Enabled = false;
                return;
            }

            try
            {
                var client = ServiceClients.CreateLoanClient();
                _selectedLoan = client.GetLoanById(row.LoanId);
                var schedule = client.GetLoanSchedule(row.LoanId);
                _scheduleGrid.DataSource = schedule.Select(s => new ScheduleRow
                {
                    InstallmentNumber = s.InstallmentNumber,
                    DueDate = s.DueDate,
                    PrincipalDue = s.PrincipalDue,
                    InterestDue = s.InterestDue,
                    TotalDue = s.TotalDue,
                    Status = s.Status
                }).ToList();

                _applyPaymentButton.Enabled = true;
            }
            catch (Exception ex)
            {
                ShowError("Could not load loan details", ex);
            }
        }

        private void ApplyLoanPayment()
        {
            if (_selectedLoan == null) return;

            using (var dialog = new AmountPromptDialog("Apply Loan Payment", "Payment amount:"))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    var client = ServiceClients.CreateLoanClient();
                    var result = client.ApplyPayment(_selectedLoan.LoanId, dialog.Amount);
                    MessageBox.Show(this,
                        $"Applied: principal {result.PrincipalApplied:C}, interest {result.InterestApplied:C}, late fee {result.LateFeeApplied:C}.\n" +
                        $"Remaining outstanding principal: {result.RemainingOutstandingPrincipal:C}\nLoan status: {result.LoanStatus}",
                        "Payment applied", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    LoadSelectedLoan();
                    SearchLoans();
                }
                catch (FaultException<LoanFault> ex)
                {
                    MessageBox.Show(this, ex.Detail.Message, ex.Detail.Code, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                catch (Exception ex)
                {
                    ShowError("Payment failed", ex);
                }
            }
        }

        private void OriginateLoan()
        {
            try
            {
                var loanClient = ServiceClients.CreateLoanClient();
                var accountClient = ServiceClients.CreateAccountClient();
                var products = loanClient.ListLoanProducts();

                using (var dialog = new NewLoanDialog(accountClient, products))
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;

                    var result = loanClient.OriginateLoan(dialog.CustomerId, dialog.LoanProductId, dialog.PrincipalAmount, dialog.TermMonths);
                    MessageBox.Show(this, $"Loan {result.LoanNumber} originated with {result.Schedule.Length} installments.",
                        "Loan originated", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    SearchLoans();
                }
            }
            catch (FaultException<LoanFault> ex)
            {
                MessageBox.Show(this, ex.Detail.Message, ex.Detail.Code, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                ShowError("Loan origination failed", ex);
            }
        }

        #endregion

        #region Statements tab

        private TabPage BuildStatementsTab()
        {
            var page = new TabPage("Statements");

            var lblAccount = new Label { Text = "Account number:", Location = new System.Drawing.Point(10, 15), AutoSize = true };
            _statementAccountBox = new TextBox { Location = new System.Drawing.Point(140, 12), Width = 180 };

            var lblStart = new Label { Text = "Period start:", Location = new System.Drawing.Point(10, 45), AutoSize = true };
            _statementStartPicker = new DateTimePicker { Location = new System.Drawing.Point(140, 42), Width = 180, Value = DateTime.Today.AddMonths(-1) };

            var lblEnd = new Label { Text = "Period end:", Location = new System.Drawing.Point(10, 75), AutoSize = true };
            _statementEndPicker = new DateTimePicker { Location = new System.Drawing.Point(140, 72), Width = 180, Value = DateTime.Today };

            var requestButton = new Button { Text = "Request Statement", Location = new System.Drawing.Point(140, 105), Width = 150 };
            requestButton.Click += async (s, e) => await RequestStatementAsync();

            _statementResultLabel = new Label { Location = new System.Drawing.Point(10, 140), AutoSize = true, Text = "" };

            var lblCheck = new Label { Text = "Statement ID:", Location = new System.Drawing.Point(10, 175), AutoSize = true };
            _statementIdBox = new TextBox { Location = new System.Drawing.Point(140, 172), Width = 100 };
            var checkButton = new Button { Text = "Check Status", Location = new System.Drawing.Point(250, 170), Width = 120 };
            checkButton.Click += async (s, e) => await CheckStatementStatusAsync();

            _openPdfButton = new Button { Text = "Open PDF", Location = new System.Drawing.Point(380, 170), Width = 100, Enabled = false };
            _openPdfButton.Click += (s, e) =>
            {
                if (!string.IsNullOrEmpty(_lastStatementFilePath))
                    Process.Start(_lastStatementFilePath);
            };

            page.Controls.AddRange(new Control[]
            {
                lblAccount, _statementAccountBox, lblStart, _statementStartPicker, lblEnd, _statementEndPicker,
                requestButton, _statementResultLabel, lblCheck, _statementIdBox, checkButton, _openPdfButton
            });
            return page;
        }

        private async System.Threading.Tasks.Task RequestStatementAsync()
        {
            try
            {
                var client = new StatementsClient();
                var result = await client.RequestStatementAsync(new StatementRequestModel
                {
                    AccountNumber = _statementAccountBox.Text.Trim(),
                    PeriodStart = _statementStartPicker.Value.Date,
                    PeriodEnd = _statementEndPicker.Value.Date
                });

                _statementResultLabel.Text = $"Requested statement #{result.StatementId}, status: {result.Status}";
                _statementIdBox.Text = result.StatementId.ToString();
            }
            catch (Exception ex)
            {
                ShowError("Statement request failed", ex);
            }
        }

        private async System.Threading.Tasks.Task CheckStatementStatusAsync()
        {
            if (!int.TryParse(_statementIdBox.Text.Trim(), out var statementId))
            {
                MessageBox.Show(this, "Enter a numeric statement ID.", "Invalid input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var client = new StatementsClient();
                var result = await client.GetStatusAsync(statementId);
                _statementResultLabel.Text = $"Statement #{result.StatementId}: {result.Status}" +
                    (result.Status == "Failed" ? $" ({result.FailureReason})" : "");

                _lastStatementFilePath = result.Status == "Completed" ? result.FilePath : null;
                _openPdfButton.Enabled = _lastStatementFilePath != null;
            }
            catch (Exception ex)
            {
                ShowError("Statement status check failed", ex);
            }
        }

        #endregion

        #region Notifications tab

        private TabPage BuildNotificationsTab()
        {
            var page = new TabPage("Notifications");

            var refreshButton = new Button { Text = "Refresh", Location = new System.Drawing.Point(10, 10), Width = 90 };
            refreshButton.Click += (s, e) => LoadNotifications();

            _notificationsGrid = new DataGridView
            {
                Location = new System.Drawing.Point(10, 45),
                Size = new System.Drawing.Size(960, 500),
                ReadOnly = true,
                AutoGenerateColumns = true,
                AllowUserToAddRows = false
            };

            page.Controls.AddRange(new Control[] { refreshButton, _notificationsGrid });
            return page;
        }

        private void LoadNotifications()
        {
            try
            {
                var repository = new NotificationsReadRepository();
                _notificationsGrid.DataSource = repository.GetRecent();
            }
            catch (Exception ex)
            {
                ShowError("Could not load notifications", ex);
            }
        }

        #endregion

        private void ShowError(string title, Exception ex)
        {
            MessageBox.Show(this, ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
