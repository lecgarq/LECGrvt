---
description: Install GSD Antigravity template correctly via GitHub repository
---

# /install-cc Workflow

<objective>
Installs the GSD template for Antigravity correctly using the explicit fallback script from toonight/get-shit-done-for-antigravity.
</objective>

<process>

// turbo-all

## 1. Clone the GSD Template

**PowerShell:**
```powershell
if (Test-Path gsd-template) { Remove-Item -Recurse -Force gsd-template }
git clone https://github.com/toonight/get-shit-done-for-antigravity.git gsd-template
```

## 2. Copy the Template to Project

**PowerShell:**
```powershell
Copy-Item -Path gsd-template\.agent -Destination .\ -Recurse -Force
Copy-Item -Path gsd-template\.gemini -Destination .\ -Recurse -Force
Copy-Item -Path gsd-template\.gsd -Destination .\ -Recurse -Force
Copy-Item -Path gsd-template\adapters -Destination .\ -Recurse -Force
Copy-Item -Path gsd-template\docs -Destination .\ -Recurse -Force
Copy-Item -Path gsd-template\scripts -Destination .\ -Recurse -Force
Copy-Item -Path gsd-template\PROJECT_RULES.md -Destination .\ -Force
Copy-Item -Path gsd-template\GSD-STYLE.md -Destination .\ -Force
Copy-Item -Path gsd-template\model_capabilities.yaml -Destination .\ -Force
```

## 3. Clean Up

**PowerShell:**
```powershell
Remove-Item -Recurse -Force gsd-template
```

</process>
