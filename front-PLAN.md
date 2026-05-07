# My SSH WPF UI Polish Plan: macOS-Like SaaS Desktop

## Summary
- Polish the current WPF UI into a more macOS-like, modern SaaS desktop experience: light sidebar, clean content surfaces, softer hierarchy, consistent spacing, restrained blue accent, refined controls, and clearer empty states.
- Preserve all existing commands, bindings, ViewModels, services, data flow, and business behavior. Changes stay in XAML styling/layout and reusable WPF resources.
- Replace the original browser/mobile QA requirement with WPF desktop visual checks at default size, minimum window size, and dialog sizes.

## Current Visual Issues
- Visual system is thin: colors, typography, spacing, borders, and control states are defined inconsistently across [App.xaml](</mnt/c/Users/woo/Documents/New project 3/src/MySSH/App.xaml>) and [MainWindow.xaml](</mnt/c/Users/woo/Documents/New project 3/src/MySSH/MainWindow.xaml>).
- Current dark sidebar feels heavier than the desired Apple/macOS style and competes with the main workspace.
- Cards are plain bordered `Border`s without consistent radius, shadow, padding, or content rhythm.
- Buttons, inputs, ComboBoxes, DataGrid rows, tabs, and ListBox nav items lack polished hover/pressed/focus/disabled states.
- Dense tables and toolbars feel crowded; dialogs use functional grids but need better label rhythm, field sizing, and action placement.
- Empty states exist but are visually plain; status/error/loading-like feedback is not presented with a unified style.

## Key Changes
- Add a small WPF theme layer, preferably `src/MySSH/Resources/Theme.xaml`, merged from `App.xaml`, with design tokens for:
  - macOS-like colors: window background `#F5F5F7`, surface white, sidebar `#F2F2F7`, text `#1D1D1F`, secondary text `#6E6E73`, separator `#D2D2D7`, accent `#007AFF`.
  - typography styles for page titles, section titles, body text, captions, labels, and mono terminal text.
  - spacing constants for page padding, panel padding, toolbar gaps, card gaps, and form row spacing.
- Restyle global controls:
  - Buttons: 8-10px radius, 32-36px height, consistent padding, primary/secondary/destructive variants, polished hover/pressed/focus/disabled states.
  - TextBox/ComboBox: rounded field chrome, clear focus ring, consistent height, internal padding, softer borders.
  - DataGrid: cleaner headers, taller rows, horizontal separators only, hover/selected row states, better cell padding.
  - ListBox/TabControl/StatusBar: lighter chrome, selected pill states, reduced visual noise.
- Rework main layout:
  - Convert the sidebar from dark navigation to a macOS-style light sidebar with selected rounded item, calmer app identity area, and status text separated from navigation.
  - Use full-width content bands or standalone panels, avoiding nested card-on-card composition.
  - Standardize page headers with title, supporting copy, and right-aligned actions.
  - Improve home dashboard cards with consistent size, padding, hierarchy, and primary action emphasis.
  - Improve SSH terminal area while keeping the terminal itself dark: cleaner server list panel, tab styling, input/action row spacing.
- Rework dialogs:
  - Polish server editor and import dialogs with consistent form labels, field heights, panel padding, footer action bar, and table styling.
  - Keep all existing bindings and click handlers unchanged.

## UX States
- Empty states: refine “no servers” and “no terminal” states with calmer typography, centered layout, and clear primary/secondary actions.
- Loading/error/status: style existing status text, update status, operation log, and disabled command states consistently without adding new business logic.
- Hover/focus: ensure every clickable/control surface has visible hover and keyboard focus states.
- Resize behavior: verify no overlap at `MinWidth=980`, dialogs at minimum sizes, and long paths/log text wrap or scroll cleanly.

## Test Plan
- Build with `dotnet build MySSH.sln -c Debug -p:Platform=x64`.
- Run the WPF app and visually inspect:
  - Main window at `1180x760`.
  - Main window at minimum `980x640`.
  - Each section: 主页、服务器信息、SSH 连接、服务器密钥、设置。
  - Server editor dialog and SSH config import dialog.
- Check interaction states manually: nav selected/hover, button hover/pressed/disabled, text input focus, ComboBox focus, DataGrid hover/selection, tabs, terminal input row.
- Confirm all existing actions still invoke the same commands and no data/business logic files were changed.

## Assumptions
- Target is the current WPF desktop app, not a web/Tailwind frontend.
- “Apple kind” means visibly macOS-like: light sidebar, soft panels, rounded controls, restrained blue accent, and quiet premium spacing.
- No heavy UI library will be introduced.
- Browser and mobile checks are not applicable to this WPF app; the equivalent validation is desktop window resizing and dialog visual QA.
