# Usage Examples

Real-world scenarios demonstrating how to use the WpfPilot MCP Server tools effectively.

---

## Example 1: Attaching to an Application

### Scenario
You want to automate WPFapp, which is already running.

### Conversation

```
User: "Connect to WPFapp"

AI uses:
→ wpf_list_apps
  Returns: [{ pid: 8432, name: "WPFapp", title: "WPFapp - Dashboard" }, ...]
  
→ wpf_attach(processName: "WPFapp")
  Returns: { sessionId: "sess_01", processId: 8432, mainWindowTitle: "WPFapp - Dashboard" }

→ wpf_snapshot(maxDepth: 3)
  Returns: { window: "WPFapp - Dashboard", elements: [...] }

AI: "Connected to WPFapp (PID 8432). The main window shows a dashboard with 
     navigation panel, data grid, and toolbar."
```

---

## Example 2: Filling Out a Form

### Scenario
Create a new project by filling in form fields and clicking Save.

### Conversation

```
User: "Create a new project named 'Q4 Release' with priority High"

AI uses:
→ wpf_invoke(automationId: "btnNewProject")
→ wpf_wait_for_window(title: "New Project")
→ wpf_snapshot(maxDepth: 4)

→ wpf_set_value(automationId: "txtProjectName", value: "Q4 Release")
→ wpf_expand(automationId: "cmbPriority")
→ wpf_select(automationId: "cmbPriority", name: "High")

→ wpf_assert_enabled(automationId: "btnSave")
→ wpf_invoke(automationId: "btnSave")

→ wpf_wait_for_absent(automationId: "NewProjectWindow")

AI: "Done. Created project 'Q4 Release' with High priority. The dialog closed 
     successfully."
```

---

## Example 3: Diagnosing a Disabled Button

### Scenario
The user can't figure out why a button is grayed out.

### Conversation

```
User: "Why is the Submit button disabled?"

AI uses:
→ wpf_get_state(automationId: "btnSubmit")
  Returns: { enabled: false, visible: true, focused: false }

→ wpf_probe_status
  Returns: { connected: true }

→ wpf_get_command_state(commandName: "SubmitCommand")
  Returns: { canExecute: false, commandType: "RelayCommand" }

→ wpf_get_validation_state
  Returns: { 
    hasErrors: true, 
    errors: [
      { property: "Email", message: "Email is required" },
      { property: "Amount", message: "Amount must be greater than 0" }
    ]
  }

AI: "The Submit button is disabled because its bound SubmitCommand.CanExecute 
     returns false. There are 2 validation errors:
     1. 'Email is required' on the Email field
     2. 'Amount must be greater than 0' on the Amount field
     
     Fix these validation errors and the button will enable."
```

---

## Example 4: Recording a Workflow

### Scenario
Record a multi-step workflow for later replay or test generation.

### Conversation

```
User: "Record me creating an invoice"

AI uses:
→ wpf_record_start(name: "Create Invoice")
  Returns: { recording: true, name: "Create Invoice" }

→ wpf_open_menu_path(path: "File > New > Invoice")
→ wpf_record_step(name: "Open new invoice dialog")

→ wpf_wait_for_window(title: "New Invoice")
→ wpf_set_value(automationId: "txtCustomer", value: "Acme Corp")
→ wpf_set_value(automationId: "txtAmount", value: "1500.00")
→ wpf_select(automationId: "cmbCurrency", name: "USD")
→ wpf_record_step(name: "Fill invoice details")

→ wpf_record_assertion(automationId: "txtTotal", expectedText: "$1,500.00")
→ wpf_invoke(automationId: "btnSave")
→ wpf_wait_for_absent(title: "New Invoice")

→ wpf_record_stop
  Returns: { steps: 8, assertions: 1, duration: "45s", json: {...} }

AI: "Recording complete! Captured 8 steps with 1 assertion. 
     I can export this as a test or replay it later."
```

### Exporting as Test Code

```
User: "Export that as an xUnit test"

→ wpf_export_test(framework: "xunit")
  Returns: C# test code

AI: "Here's your generated test:
```csharp
[Fact]
public void CreateInvoice_ShouldSucceed()
{
    // Arrange
    var app = Application.Attach("WPFapp");
    var window = app.GetMainWindow(automation);
    
    // Act - Open new invoice
    window.FindFirstDescendant(cf => cf.ByAutomationId("mnuFile")).Click();
    window.FindFirstDescendant(cf => cf.ByName("New")).Click();
    window.FindFirstDescendant(cf => cf.ByName("Invoice")).Click();
    
    var dialog = app.GetAllTopLevelWindows(automation)
        .First(w => w.Title == "New Invoice");
    
    dialog.FindFirstDescendant(cf => cf.ByAutomationId("txtCustomer"))
        .AsTextBox().Text = "Acme Corp";
    dialog.FindFirstDescendant(cf => cf.ByAutomationId("txtAmount"))
        .AsTextBox().Text = "1500.00";
    // ... more steps ...
    
    // Assert
    var total = dialog.FindFirstDescendant(cf => cf.ByAutomationId("txtTotal"));
    Assert.Equal("$1,500.00", total.AsLabel().Text);
}
```"
```

---

## Example 5: Accessibility Audit

### Scenario
Check a window for accessibility issues before release.

### Conversation

```
User: "Run an accessibility audit on the Settings page"

AI uses:
→ wpf_select_window(title: "Settings")
→ wpf_check_missing_names
  Returns: { count: 3, elements: [
    { controlType: "Button", className: "IconButton", hint: "toolbar row 2, position 3" },
    { controlType: "Image", className: "Image", hint: "logo area" },
    { controlType: "TextBox", className: "TextBox", hint: "below 'API Key' label" }
  ]}

→ wpf_check_keyboard_access
  Returns: { unreachable: 1, elements: [
    { controlType: "Button", name: "Quick Action", reason: "Focusable=false" }
  ]}

→ wpf_check_tab_order
  Returns: { issues: ["Tab order skips from 'Username' to 'Save', missing 'Password'"] }

AI: "Accessibility audit for Settings page found 5 issues:

     **Missing accessible names (3):**
     - IconButton in toolbar (no Name or AutomationId)
     - Image in logo area (decorative? add Name or mark as decorative)
     - TextBox below 'API Key' label (needs AutomationProperties.Name)
     
     **Keyboard access (1):**
     - 'Quick Action' button has Focusable=false — keyboard users can't reach it
     
     **Tab order (1):**
     - Tab order jumps from 'Username' to 'Save', skipping 'Password' field
     
     Severity: 2 critical (keyboard access, missing TextBox name), 3 moderate."
```

---

## Example 6: DataGrid Operations

### Scenario
Find and edit a specific row in a data grid.

### Conversation

```
User: "Find the order for customer 'Acme Corp' and change status to 'Shipped'"

AI uses:
→ wpf_grid_get_columns(automationId: "dgOrders")
  Returns: ["OrderId", "Customer", "Date", "Status", "Total"]

→ wpf_grid_find_row(automationId: "dgOrders", columnValues: { "Customer": "Acme Corp" })
  Returns: { rowIndex: 7, cells: { OrderId: "ORD-2024-089", Customer: "Acme Corp", 
             Date: "2024-01-15", Status: "Processing", Total: "$3,200" } }

→ wpf_grid_select_row(automationId: "dgOrders", rowIndex: 7)
→ wpf_grid_set_cell(automationId: "dgOrders", rowIndex: 7, columnIndex: 3, value: "Shipped")

→ wpf_assert_grid_cell(automationId: "dgOrders", rowIndex: 7, columnIndex: 3, 
                        expectedValue: "Shipped")

AI: "Updated order ORD-2024-089 (Acme Corp) status from 'Processing' to 'Shipped'."
```

---

## Example 7: Multi-Window Workflow

### Scenario
Work across multiple windows — main window and a dialog.

### Conversation

```
User: "Export the report to PDF and save to Desktop"

AI uses:
→ wpf_open_menu_path(path: "File > Export > PDF")
→ wpf_wait_for_window(title: "Export Options")

→ wpf_list_windows
  Returns: [
    { title: "Report Viewer", handle: "0x1234", isMain: true },
    { title: "Export Options", handle: "0x5678", isModal: true }
  ]

→ wpf_select_window(title: "Export Options")
→ wpf_set_value(automationId: "txtPath", value: "C:\\Users\\user\\Desktop\\report.pdf")
→ wpf_check(automationId: "chkOpenAfter")
→ wpf_invoke(automationId: "btnExport")

→ wpf_wait_for_absent(title: "Export Options")

AI: "Report exported to Desktop as report.pdf. The export dialog closed and the 
     file will open automatically."
```

---

## Example 8: Detecting UI Quality Issues

### Scenario
Audit the app for automation quality before writing tests.

### Conversation

```
User: "How testable is this application?"

AI uses:
→ wpf_detect_missing_ids
  Returns: { count: 12, elements: [...] }

→ wpf_detect_duplicate_ids
  Returns: { count: 2, duplicates: ["btnAction", "txtInput"] }

→ wpf_analyze_automation_quality
  Returns: { 
    score: 72,
    breakdown: {
      automationIdCoverage: "68%",
      duplicateIds: 2,
      missingNames: 5,
      keyboardAccessible: "95%"
    },
    recommendations: [
      "Add AutomationId to 12 actionable elements",
      "Fix duplicate IDs: btnAction, txtInput",
      "Add accessible names to 5 image buttons"
    ]
  }

AI: "Testability score: **72/100**

     **Good:**
     - 68% of elements have AutomationIds
     - 95% keyboard accessible
     
     **Issues:**
     - 12 actionable elements missing AutomationId
     - 2 duplicate IDs (btnAction, txtInput) — will cause selector ambiguity
     - 5 image buttons missing accessible names
     
     **Recommended fixes for dev team:**
     1. Add unique AutomationIds to buttons/inputs (biggest impact)
     2. Rename duplicate 'btnAction' and 'txtInput' IDs
     3. Set AutomationProperties.Name on icon-only buttons"
```

---

## Example 9: Safe Action Preview

### Scenario
Preview a potentially destructive action before executing.

### Conversation

```
User: "Delete all completed tasks"

AI uses:
→ wpf_set_policy(allowDestructive: false)
→ wpf_preview_action(action: "click", automationId: "btnDeleteCompleted")
  Returns: { 
    preview: true,
    wouldDo: "Invoke button 'Delete Completed Tasks'",
    element: { automationId: "btnDeleteCompleted", name: "Delete Completed Tasks" },
    warning: "Policy: destructive actions require confirmation"
  }

AI: "This would click 'Delete Completed Tasks'. Since destructive actions are 
     restricted by policy, shall I proceed?"

User: "Yes, go ahead"

→ wpf_confirm_action
→ wpf_wait_until_snapshot_stable(stabilityMs: 1000)

AI: "Done. Deleted completed tasks. The list has been updated."
```

---

## Example 10: Open WPFapp and Create a New Patient

### Scenario
Launch WPFapp, navigate to patient management, and create a new patient record.

### Conversation

```
User: "Open WPFapp and create a new patient"

AI uses:
→ wpf_launch_app(path: "C:\\Program Files\\WPFapp\\WPFapp.exe")
  Returns: { sessionId: "sess_10", processId: 9210, mainWindowTitle: "WPFapp - Login" }

→ wpf_wait_for_window(title: "WPFapp - Login", timeoutMs: 10000)
→ wpf_snapshot(maxDepth: 3)
  Returns: { window: "WPFapp - Login", elements: [
    { type: "TextBox", automationId: "txtUsername" },
    { type: "PasswordBox", automationId: "txtPassword" },
    { type: "Button", automationId: "btnLogin", name: "Sign In" }
  ]}

AI: "WPFapp is open at the login screen. Do you want me to log in first?"

User: "Yes, user admin / admin123"

→ wpf_set_value(automationId: "txtUsername", value: "admin")
→ wpf_set_value(automationId: "txtPassword", value: "admin123")
→ wpf_invoke(automationId: "btnLogin")

→ wpf_wait_for_window(title: "WPFapp - Dashboard", timeoutMs: 15000)
→ wpf_snapshot(maxDepth: 3)

AI: "Logged in. Now on the Dashboard. Navigating to Patient Management..."

→ wpf_open_menu_path(path: "Patients > New Patient")
  OR
→ wpf_invoke(automationId: "btnNewPatient")

→ wpf_wait_for_window(title: "New Patient", timeoutMs: 5000)
→ wpf_snapshot(maxDepth: 4)
  Returns: { window: "New Patient", elements: [
    { type: "TextBox", automationId: "txtFirstName", name: "First Name" },
    { type: "TextBox", automationId: "txtLastName", name: "Last Name" },
    { type: "DatePicker", automationId: "dpDateOfBirth", name: "Date of Birth" },
    { type: "ComboBox", automationId: "cmbGender", name: "Gender" },
    { type: "TextBox", automationId: "txtPhone", name: "Phone" },
    { type: "TextBox", automationId: "txtEmail", name: "Email" },
    { type: "TextBox", automationId: "txtAddress", name: "Address" },
    { type: "Button", automationId: "btnSave", name: "Save" },
    { type: "Button", automationId: "btnCancel", name: "Cancel" }
  ]}

AI: "The New Patient form is open. What patient details should I fill in?"

User: "John Smith, born 1985-03-15, male, phone 555-0123"

→ wpf_set_value(automationId: "txtFirstName", value: "John")
→ wpf_set_value(automationId: "txtLastName", value: "Smith")
→ wpf_set_date(automationId: "dpDateOfBirth", value: "03/15/1985")
→ wpf_expand(automationId: "cmbGender")
→ wpf_select(automationId: "cmbGender", name: "Male")
→ wpf_set_value(automationId: "txtPhone", value: "555-0123")

→ wpf_assert_enabled(automationId: "btnSave")
→ wpf_invoke(automationId: "btnSave")

→ wpf_wait_for_absent(title: "New Patient")
→ wpf_wait_until_snapshot_stable(stabilityMs: 1000)

AI: "Patient created successfully:
     - Name: John Smith
     - DOB: March 15, 1985
     - Gender: Male
     - Phone: 555-0123
     
     The form closed and we're back on the main screen."
```

### With Probe — Verifying Save Success

```
→ wpf_probe_connect
→ wpf_get_viewmodel_properties(windowTitle: "WPFapp - Dashboard")
  Returns: { 
    LastCreatedPatientId: "PAT-2024-0042",
    StatusMessage: "Patient 'John Smith' created successfully" 
  }

AI: "Confirmed via ViewModel: Patient ID PAT-2024-0042 was created."
```

---

## Tips and Best Practices

### 1. Always Snapshot First
Before performing actions, take a snapshot to understand the current UI state.

### 2. Use Waits Between Actions  
After clicking a button that opens a dialog, always use `wpf_wait_for_window` before interacting with the new window.

### 3. Prefer AutomationId
When asking the AI to interact with elements, referring to them by AutomationId produces the most reliable automation.

### 4. Check Probe for "Why" Questions
If something is unexpectedly disabled or showing wrong data, the probe can inspect ViewModel state directly.

### 5. Record Complex Workflows
For multi-step processes you'll repeat, record them once and replay/export as tests.
