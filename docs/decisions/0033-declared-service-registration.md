# 0033 服務由類別自己宣告，組合產生器註冊

- **狀態**：採用（2026-11-03）
- **決定**：
  - 實作組合擴充點介面的類別由 `FeatureCompositionGenerator` 註冊，模組不再手寫。擴充點是 Platform 標了 `[RegisterImplementations(註冊類別, 方法)]` 的介面：`IMonitoringSource`、`IDashboardSource`、`IAccountReviewSource`、`IDomainEventHandler<T>`。框架的 `IHostedService` 是內建的另一個擴充點。
  - 一般服務在類別上標 `[Scoped]`、`[Singleton]`；以介面註冊時標 `[Scoped<IFoo>]`、`[Singleton<IFoo>]`。
  - 模組的 `AddServices` 只寫需要參數或判斷的註冊：設定、rate limit、HttpClient、外部資料庫、feature policy、背景工作種類、寄信範例、工廠與轉接。
  - 擴充點的實作若再手動註冊，由 `ManualRegistrationAnalyzer`（AINX1007）讓建置失敗；生命週期標記不能建立或沒有實作所標的介面時，回報 AINX1006。
- **延伸**：[ADR 0029](0029-compile-time-composition.md)（同一個產生器、同一套排序與範圍）。

## 原因

- **型別本身已經說明怎麼註冊**：盤點時 30 個模組的 `AddServices` 約 230 行，其中約 170 行可以從型別推出（實作哪個介面、哪個生命週期）。`AddDomainEventHandler<TEvent, THandler>` 的 `TEvent` 甚至寫了兩次。新增一個來源要改兩個檔案，漏寫時編譯照過，到執行期才發現少了區塊或少了處理器。
- **擴充點自己描述註冊方式**：屬性標在介面上，產生器不認得任何 Platform 介面。新增擴充點只要在介面加屬性，不改產生器。怎麼註冊仍寫在 Platform 的擴充方法裡，產出只是直接呼叫（[Source generator](../architecture/source-generators.md) 第 4 點）。
- **生命週期寫在類別旁邊**：讀類別時就看得到它是 singleton 還是 scoped，不必翻模組檔。生命週期仍由開發者明確決定，不依命名或資料夾推測。
- **一條路**：自動註冊和手動註冊並存時，監控來源、總覽區塊會悄悄出現兩次（它們的註冊不去重），所以由 analyzer 禁止手動呼叫，而不是讓註冊方法改成冪等、容忍兩種寫法。
- **端點掛載不變**：路由群組集中決定授權與 OpenAPI 順序，理由見 ADR 0029。

## 取捨

- 模組檔不再列出全部服務。要看模組註冊了什麼，看產生的 `FeatureModules.g.cs`，或搜尋 `[Scoped]`、`[Singleton]`。
- 生命週期錯配（singleton 注入 scoped）分散在各類別，靠容器的範圍驗證發現，不再能從模組檔一眼看出。
- 介面與類別要共用同一個實例時（`AddScoped<IFoo>(sp => sp.GetRequiredService<Foo>())`），標記表達不了，仍在模組以工廠註冊。
- 註冊順序由各模組的宣告順序改為依實作型別名稱，監控來源與 hosted service 的順序因此改變；消費端本來就不可依賴順序（監控依 `Order` 排序，hosted service 並行啟動）。
- 遷移時以變更前後的 `IServiceCollection` 比對，903 筆註冊完全相同。各模組的一般服務改為標記是逐步進行的，未遷移的手寫註冊仍然有效：標記用 TryAdd，模組自己註冊的同一型別優先。

## 何時重新評估

- 擴充點需要依設定決定要不要註冊時：在介面的註冊方法裡判斷，或改回模組手寫並移除該介面的屬性。
