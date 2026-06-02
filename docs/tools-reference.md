# Tool Reference

Complete reference for all 131 MCP tools provided by the WpfPilot MCP Server.

---

## Session Management (11 tools)

Tools for attaching to, launching, and managing WPF application processes and windows.

| Tool | Description |
|------|-------------|
| `wpf_list_apps` | List candidate WPF/Windows desktop processes and top-level windows |
| `wpf_launch_app` | Launch app from executable path with optional args and working directory |
| `wpf_attach` | Attach to a running process/window by PID, process name, or window title |
| `wpf_detach` | Detach from current app session |
| `wpf_session_status` | Return current attachment, window handle, PID, app state, active window |
| `wpf_focus_window` | Bring attached app/window to foreground |
| `wpf_list_windows` | List top-level, modal, popup, owned, and child windows for attached process |
| `wpf_select_window` | Switch active target window within attached process by title or automation id |
| `wpf_close_window` | Close a window through normal close command |
| `wpf_get_window_state` | Get minimized/maximized/normal/focused/modal state |
| `wpf_set_window_state` | Minimize, maximize, or restore a window |

### Example

```
User: "Attach to WPFapp"
→ wpf_list_apps → finds PID 12345 "WPFapp"
→ wpf_attach(processName: "WPFapp")
→ { sessionId: "abc123", processId: 12345, mainWindowTitle: "WPFapp - Main" }
```

---

## UI Inspection / Snapshots (14 tools)

Tools for capturing and querying the UI automation tree.

| Tool | Description |
|------|-------------|
| `wpf_snapshot` | Return compact UI tree for current window |
| `wpf_snapshot_element` | Return subtree for one element by automation id or name |
| `wpf_query` | Find elements by AutomationId, name, control type, or class name (first match) |
| `wpf_query_all` | Find all elements matching criteria |
| `wpf_get_element` | Resolve a selector to one element and return its properties |
| `wpf_get_properties` | Get full UIA properties for an element |
| `wpf_get_patterns` | List supported UIA patterns for an element |
| `wpf_get_text` | Get visible text from element |
| `wpf_get_value` | Get value from TextBox, ComboBox, Slider, DatePicker, etc. |
| `wpf_get_state` | Get element state: enabled, visible, focused, selected, expanded, checked, read-only, offscreen |
| `wpf_get_bounds` | Get screen/window-relative bounding box for an element |
| `wpf_get_selection` | Get selected item(s) from list/grid/tree/combo |
| `wpf_diff_snapshot` | Compare two snapshots and report added/removed/changed elements |
| `wpf_watch_ui_changes` | Monitor UI tree changes for a short interval and report differences |

### Key Parameters

- `automationId` — Preferred selector strategy (most stable)
- `name` — Element's accessible name
- `controlType` — UIA control type (Button, TextBox, ComboBox, etc.)
- `className` — WPF class name
- `maxDepth` — How deep to traverse the tree (default: 5)

---

## Actions (26 tools)

Tools for interacting with UI elements — clicking, typing, selecting, dragging.

| Tool | Description |
|------|-------------|
| `wpf_invoke` | Invoke Button, MenuItem, Hyperlink through UIA InvokePattern |
| `wpf_click` | Click element using pattern if available, coordinates as fallback |
| `wpf_double_click` | Double-click element |
| `wpf_right_click` | Right-click element to open context menu |
| `wpf_set_value` | Set text/value via ValuePattern |
| `wpf_clear_value` | Clear text/value from element |
| `wpf_type_text` | Type text into focused or selected element via keyboard input |
| `wpf_send_keys` | Send keyboard shortcuts (e.g., Ctrl+S, Enter, Tab) |
| `wpf_focus` | Move focus to element |
| `wpf_select` | Select list/grid/tree/combo item by automation id or name |
| `wpf_select_by_text` | Select item in a list/combo by visible text |
| `wpf_select_by_index` | Select item by index (marked as brittle) |
| `wpf_toggle` | Toggle checkbox, toggle button, or expander |
| `wpf_check` | Ensure checkbox is checked |
| `wpf_uncheck` | Ensure checkbox is unchecked |
| `wpf_expand` | Expand combo/tree/expander/menu |
| `wpf_collapse` | Collapse combo/tree/expander/menu |
| `wpf_scroll_into_view` | Scroll element into view |
| `wpf_scroll` | Scroll container by direction and amount |
| `wpf_open_menu_path` | Open menu path such as "File > Export > PDF" |
| `wpf_open_context_menu_item` | Right-click target and invoke context menu item |
| `wpf_drag_drop` | Drag source element to target element |
| `wpf_set_slider` | Set Slider/RangeBase value |
| `wpf_set_date` | Set DatePicker/Calendar date by typing text value |
| `wpf_accept_dialog` | Click OK/Yes/Accept on current modal dialog |
| `wpf_cancel_dialog` | Click Cancel/No/Close on current modal dialog |

### Selector Strategy

Actions resolve elements using this priority:
1. **AutomationId** — most stable, survives refactoring
2. **Name** — accessible name, may change with localization
3. **ControlType + ClassName** — structural, less specific
4. **Index/Coordinates** — last resort, marked as brittle

---

## Wait Conditions (11 tools)

Tools for polling until UI reaches a desired state.

| Tool | Description |
|------|-------------|
| `wpf_wait_for` | Generic wait: selector exists and/or has specific state |
| `wpf_wait_for_element` | Wait until element exists (up to timeout ms) |
| `wpf_wait_for_absent` | Wait until element disappears |
| `wpf_wait_for_enabled` | Wait until element is enabled |
| `wpf_wait_for_disabled` | Wait until element is disabled |
| `wpf_wait_for_visible` | Wait until element is visible/not offscreen |
| `wpf_wait_for_hidden` | Wait until element is hidden/offscreen |
| `wpf_wait_for_text` | Wait until element text equals or contains expected value |
| `wpf_wait_for_value` | Wait until element value matches expected |
| `wpf_wait_for_window` | Wait for a window/dialog with title or automation id |
| `wpf_wait_until_snapshot_stable` | Wait until UI tree stops changing for stabilityMs |

### Parameters

- `timeoutMs` — Maximum wait time (default: 10000ms)
- `intervalMs` — Poll interval (default: 200ms)

---

## Assertions (15 tools)

Tools for verifying UI state — useful in test workflows and recording assertions.

| Tool | Description |
|------|-------------|
| `wpf_assert_exists` | Assert element exists |
| `wpf_assert_not_exists` | Assert element is absent |
| `wpf_assert_visible` | Assert element is visible (not offscreen) |
| `wpf_assert_enabled` | Assert element is enabled |
| `wpf_assert_disabled` | Assert element is disabled |
| `wpf_assert_text` | Assert element text equals, contains, or matches expected |
| `wpf_assert_value` | Assert element value equals expected |
| `wpf_assert_checked` | Assert checkbox/toggle is checked |
| `wpf_assert_unchecked` | Assert checkbox/toggle is unchecked |
| `wpf_assert_no_validation_errors` | Assert no validation errors visible in window |
| `wpf_assert_selected` | Assert specific item is selected in list/combo/tree |
| `wpf_assert_grid_row_count` | Assert DataGrid/ListView row count (supports gt, lt, gte, lte) |
| `wpf_assert_grid_cell` | Assert grid cell contains expected value |
| `wpf_assert_accessibility` | Assert accessibility: name/automationId present, keyboard focusable |
| `wpf_assert_snapshot_matches` | Compare current UI to baseline snapshot within tolerance |

---

## Selectors (6 tools)

Tools for generating, validating, and healing element selectors.

| Tool | Description |
|------|-------------|
| `wpf_build_selector` | Generate stable selector for an element |
| `wpf_validate_selector` | Test whether a selector resolves uniquely |
| `wpf_explain_selector` | Explain how selector is resolved and why it may be brittle |
| `wpf_rank_selectors` | Generate multiple selectors ranked by stability |
| `wpf_detect_missing_ids` | Find actionable elements missing AutomationId |
| `wpf_detect_duplicate_ids` | Find duplicate AutomationIds in current window |

---

## DataGrid & Tree (12 tools)

Specialized tools for DataGrid, ListView, and TreeView controls.

| Tool | Description |
|------|-------------|
| `wpf_grid_get_rows` | Return visible DataGrid/ListView rows |
| `wpf_grid_get_columns` | Return column headers and metadata |
| `wpf_grid_get_cell` | Get cell value by row and column index |
| `wpf_grid_set_cell` | Edit cell value by row and column index |
| `wpf_grid_select_row` | Select row by index or by cell text |
| `wpf_grid_double_click_row` | Double-click a row by index |
| `wpf_grid_find_row` | Find row by column values |
| `wpf_grid_sort_by_column` | Click column header to sort |
| `wpf_grid_scroll_to_row` | Scroll grid until row with given text is visible |
| `wpf_tree_get_nodes` | Return visible TreeView nodes |
| `wpf_tree_expand_path` | Expand tree path such as "Settings > Network > Devices" |
| `wpf_tree_select_path` | Select tree node by path |

---

## Recording & Replay (13 tools)

Tools for recording UI workflows and replaying them deterministically.

| Tool | Description |
|------|-------------|
| `wpf_record_start` | Start recording UI actions |
| `wpf_record_stop` | Stop recording and return workflow JSON |
| `wpf_record_pause` | Pause the current recording |
| `wpf_record_resume` | Resume a paused recording |
| `wpf_record_step` | Manually add a named step/checkpoint |
| `wpf_record_assertion` | Add assertion from current UI state |
| `wpf_replay` | Replay recorded workflow JSON |
| `wpf_replay_step` | Replay one step by index or name |
| `wpf_validate_recording` | Check for brittle selectors and missing waits |
| `wpf_optimize_recording` | Replace sleeps/coordinates with waits/semantic selectors |
| `wpf_export_test` | Generate xUnit + FlaUI test code |
| `wpf_export_recording` | Export workflow as JSON |
| `wpf_import_recording` | Load and validate workflow JSON |

### Workflow JSON Schema

```json
{
  "schemaVersion": "0.1",
  "name": "Create New Project",
  "app": { "process": "WPFapp" },
  "policy": { "allowCoordinateFallback": false },
  "steps": [
    { "action": "click", "selector": { "automationId": "btnNew" } },
    { "action": "set_value", "selector": { "automationId": "txtName" }, "value": "Test" },
    { "assert": "enabled", "selector": { "automationId": "btnSave" } },
    { "action": "invoke", "selector": { "automationId": "btnSave" } }
  ]
}
```

---

## Test Generation (5 tools)

Tools for generating automated test code from current UI state.

| Tool | Description |
|------|-------------|
| `wpf_export_page_object` | Generate Page Object class from current window |
| `wpf_export_selectors` | Generate selector constants for all identifiable elements |
| `wpf_export_assertions` | Generate assertion helper methods for current form state |
| `wpf_export_test_project` | Generate full test project structure (csproj + base classes) |
| `wpf_generate_smoke_test` | Generate smoke test from current window structure |

---

## Screenshots & Visual (3+ tools)

| Tool | Description |
|------|-------------|
| `wpf_screenshot` | Capture current window screenshot (base64 PNG) |
| `wpf_screenshot_element` | Capture screenshot of specific element |
| `wpf_capture_failure_artifacts` | Capture screenshot, snapshot, and diagnostics after failure |
| `wpf_annotate_screenshot` | Capture screenshot with overlay annotations |
| `wpf_get_cursor_position` | Get current mouse cursor position |
| `wpf_highlight_element` | Flash-highlight an element for debugging |
| `wpf_compare_screenshot` | Pixel-compare two images and return difference percentage |

---

## Accessibility (6 tools)

Tools for auditing accessibility compliance.

| Tool | Description |
|------|-------------|
| `wpf_accessibility_snapshot` | Accessibility-oriented UI tree with metadata |
| `wpf_check_missing_names` | Find controls missing accessible names |
| `wpf_check_missing_help_text` | Find controls missing HelpText descriptions |
| `wpf_check_tab_order` | Analyze keyboard navigation/tab order |
| `wpf_check_keyboard_access` | Find controls unreachable by keyboard |
| `wpf_check_control_patterns` | Ensure controls expose expected UIA patterns |

---

## Policy & Safety (9 tools)

Tools for controlling execution policies, auditing, and data redaction.

| Tool | Description |
|------|-------------|
| `wpf_get_capabilities` | List all available tool categories and their status |
| `wpf_set_policy` | Set execution policy (destructive, timeout, retries, coordinate fallback) |
| `wpf_get_policy` | Get current execution policy |
| `wpf_preview_action` | Dry-run: show what an action would do without executing |
| `wpf_confirm_action` | Execute a previously previewed action |
| `wpf_clear_audit_log` | Clear the audit log |
| `wpf_redact_snapshot` | Return snapshot with sensitive fields redacted |
| `wpf_set_redaction_rules` | Add redaction rules for sensitive patterns |
| `wpf_get_redaction_rules` | List current redaction rules |

---

## Reporting & Diagnostics (9 tools)

| Tool | Description |
|------|-------------|
| `wpf_get_diagnostics` | Combined diagnostic report |
| `wpf_analyze_automation_quality` | Score automation quality |
| `wpf_generate_session_report` | Session summary |
| `wpf_get_audit_log` | View audit trail |
| `wpf_generate_testability_report` | Testability report with scores |
| `wpf_generate_diagnostics_report` | Full diagnostics: audit, session, UI state |
| `wpf_export_artifacts` | Bundle session artifacts as JSON |
| `wpf_import_artifacts` | Load artifacts bundle |
| `wpf_compare_reports` | Compare two testability reports |

---

## Clipboard & Environment (6 tools)

| Tool | Description |
|------|-------------|
| `wpf_get_clipboard` | Get clipboard text content |
| `wpf_set_clipboard` | Set clipboard text |
| `wpf_clear_clipboard` | Clear clipboard |
| `wpf_get_current_culture` | Get thread culture info |
| `wpf_get_theme` | Detect Windows theme (dark/light) |
| `wpf_get_screen_info` | Get screen resolution and DPI |

---

## Probe Management (6 tools)

Tools for managing the optional in-process WPF probe connection.

| Tool | Description |
|------|-------------|
| `wpf_probe_status` | Check if probe is connected and responding |
| `wpf_probe_connect` | Connect to probe via named pipe |
| `wpf_probe_disconnect` | Disconnect from probe |
| `wpf_probe_capabilities` | List supported probe methods |
| `wpf_probe_health` | Run probe health check |
| `wpf_probe_install_instructions` | Show probe installation guide |

---

## MVVM Diagnostics (10 tools)

Deep WPF/MVVM inspection via the in-process probe. Requires probe to be installed in target app.

| Tool | Description |
|------|-------------|
| `wpf_get_viewmodel` | Get ViewModel type and property summary |
| `wpf_get_viewmodel_properties` | Get all ViewModel properties with values |
| `wpf_get_commands` | List all ICommand properties |
| `wpf_get_command_state` | Get CanExecute state of a command |
| `wpf_execute_command` | Execute an ICommand on ViewModel |
| `wpf_get_binding_errors` | Get all WPF binding errors |
| `wpf_get_bindings` | Get all active bindings |
| `wpf_get_validation_state` | Get validation errors |
| `wpf_get_dispatcher_status` | Get Dispatcher thread status |
| `wpf_get_datacontext` | Get DataContext type and value |

### Example

```
User: "Why is the Save button disabled?"
→ wpf_probe_connect
→ wpf_get_command_state(commandName: "SaveCommand")
→ { commandName: "SaveCommand", canExecute: false }
→ wpf_get_validation_state
→ { hasErrors: true, errors: [{ message: "Name is required", bindingPath: "Name" }] }
→ "The Save button is disabled because the SaveCommand.CanExecute returns false. 
   There's a validation error: 'Name is required' on the Name property."
```
