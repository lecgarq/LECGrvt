---
name: LECG Code Architecture
description: Overview of the LECG Revit Addin architecture, patterns, and best practices.
---

# LECG Revit Addin Architecture

This folder contains detailed documentation on the architectural pillars of the LECG Revit Addin.

## Documentation Index

### 1. [MVVM Pattern](MVVM_PATTERN.md)

Detailed guide on the Model-View-ViewModel pattern, including:

- **ViewModels**: `BaseViewModel`, Observable Properties, Relay Commands.
- **Views**: Window structure, DataContext binding.
- **Binding**: Best practices for XAML binding.

### 2. [Service Architecture](SERVICE_ARCHITECTURE.md)

Guide on the Dependency Injection system:

- **Services**: Creating and implementing interfaces.
- **Bootstrapper**: Registering services and types.
- **Consumption**: How to inject services into ViewModels and Commands.

### 3. [Revit Commands](REVIT_COMMANDS.md)

Best practices for the entry points:

- **RevitCommand**: The base class features.
- **Selection**: Implementing filters.
- **Transactions**: Handling database changes.

### 4. [UI Standards](UI_STANDARDS.md)

Design system and UX guidelines:

- **Styles**: Standard controls (Buttons, Inputs, Cards).
- **Resources**: Managing Icons and Strings.
- **Feedback**: Logging and Progress updates.

## High-Level structure

```text
src/
  Commands/       # Entry points (Thin wrappers)
  Configuration/  # key-value constants
  Core/           # App lifecycle & Ribbon
  Interfaces/     # Service contracts
  Services/       # Business logic
  Utils/          # Helpers
  ViewModels/     # UI State & Logic
  Views/          # WPF Windows
```
