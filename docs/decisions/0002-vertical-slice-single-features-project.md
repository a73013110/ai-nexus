# 0002 Vertical slice 與單一 Features 專案

- **狀態**：採用（2026-10）
- **決定**：後端維持三個專案（Host → Features → Platform）。業務模組都在 `AiNexus.Features`，一個 use case 一個檔案（Minimal API 端點＋handler＋validator）。不拆每模組一個 csproj，不引入 MediatR／Controllers，不做 Clean Architecture 的 Domain／Application／Infrastructure 分層，也不拆微服務。

## 原因

- 讓專案變難維護的不是 Minimal API，而是「每個 slice 長得不一樣」與「沒人管的角落」。形狀統一由架構測試強制（`SliceTests`、`SourceLayoutTests`、`EndpointConventionTests`），比專案邊界更直接。
- 模組循環已由 `ModuleBoundaryTests` 直接禁止，拆 csproj 帶來的編譯期邊界效益很小，卻讓 build 與重新命名的成本倍增。
- Minimal API＋每檔一個 use case 已經是 vertical slice；MediatR 只增加一層間接。分層專案與 vertical slice 方向相反，會把一個 use case 拆到四處。
- 一兩人維護的單體，部署成單一 IIS 網站（見 [ADR 0001](0001-single-iis-in-process-locks.md)）。

## 何時重新評估

某個模組需要獨立部署、獨立擴展，或由不同團隊維護時，再把它拆成獨立專案；此時跨模組依賴已只經 `public` 服務與 domain event，拆分成本可控。
