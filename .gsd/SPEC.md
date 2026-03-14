# SPEC.md — Project Specification

> **Status**: `FINALIZED`

## Vision
To elevate the LECG Revit 2026 Addin from a functional utility into a premium, state-of-the-art enterprise product by creating a unified, lightning-fast, and deeply branded UI/UX, while simultaneously continuing rigorous backend refactoring for maximum performance and modularity.

## Goals
1. **Design System Standardization:** Build and deploy a centralized, reusable WPF component library enforcing the LECG brand identity (custom earth-toned palette, minimalism) across all 36 views.
2. **Premium UX Features:** Ensure absolute consistency in professional interactions, including hover effects, ultra-fast real-time searching, batch selection/expansion/collapsing, and fluid layout scaling across all window sizes.
3. **Architectural Refactoring:** Continue the backend performance and modularity refactoring (guided by prior architectural plans) to perfectly decouple the heavy Services from the refined MVVM UI layer.

## Non-Goals (Out of Scope)
- No glassmorphism styling or complex translucency (strictly avoided due to Revit rendering crashes).
- No new features outside of the existing mapped utilities—this effort strictly focuses on standardizing the UI/UX and backend modularization of *existing* tools.

## Users
Architects and structural engineers utilizing the LECG Revit Addin who require extremely responsive, reliable, and visually cohesive tooling that feels distinctly "LECG" while operating within Revit 2026.

## Constraints
- **Technical Restrictions:** Must strictly avoid WPF features known to crash the Revit 2026 rendering engine pipeline (e.g., intensive glassmorphism).
- **Architecture:** Must rigidly adhere to the MVVM pattern utilizing `CommunityToolkit.Mvvm`, keeping Views strictly XAML-based and logic in ViewModels.
- **Palette:** Must build around the defined 9-color HEX system: `#E6E3DA`, `#C8C0B4`, `#A89D8E`, `#7A634F`, `#E9E9E6`, `#96938C`, `#4E4B44`, `#323130`, `#708452`.

## Success Criteria
- [ ] A proprietary `LecgUI` component library is built and replaces ad-hoc XAML in at least 3 core views.
- [ ] UI correctly scales to various monitor sizes without element clipping or distortion.
- [ ] Batch selection and real-time search execute responsively without blocking the Revit main thread.
- [ ] Codebase structure and modularity reflect the desired performance architecture, eliminating duplicated UI logic.
