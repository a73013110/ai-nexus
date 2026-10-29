# 從程式讀 CSS 值

結論：正式建置會壓縮 CSS，`getComputedStyle()` 讀到的自訂屬性是壓縮後的文字，不一定是原始碼的寫法；本機開發不壓縮，所以這類錯誤只在正式機出現。程式要用到數值時一律依 CSS 語法解析，動效權杖經過 [`motion-tokens.ts`](../../frontend/src/app/shared/browser/motion-tokens.ts)。

## 會被改寫的東西

- 未註冊的自訂屬性（沒有 `@property`）回傳的是原文，瀏覽器不換算單位；壓縮器可以改寫原文。
- 已知案例：`--motion-panel: 240ms` 在正式機讀到 `.24s`，`parseFloat` 得到 0.24，動畫等於沒播（表格放大、對話框換尺寸、滑動回彈、拖曳排序都曾中過）。
- 數字、單位、顏色的寫法都可能被縮短，不要假設和 `tokens.scss` 一字不差。

## 做法

- 動效時長與曲線：`motionTiming(element, '--motion-panel', '--ease')` 直接當 `animate()` 選項，只要毫秒用 `motionDuration()`；兩者都換算 `ms`／`s`，讀不到時長為 0（直接到位）。參數寫完整的變數名，搜尋 `--motion-panel` 就能找到 CSS 與 JS 的所有用法。
- 其他要算的值：盡量讓 CSS 直接套用、程式只量結果（`getBoundingClientRect()`、`offsetWidth`）；非讀不可就照 CSS 語法含單位解析，不要只取數字。
- 只當字串轉交的值（例：把顏色權杖傳給 Mermaid 主題）不受影響，但不要拿來比對或拆解。
- ESLint 擋下 `parseFloat`／`parseInt`／`Number` 直接包 `getPropertyValue()` 的寫法。
- 動到這類程式時，用正式設定建置（`npm --prefix frontend run build`）後再確認行為，不能只看 `ng serve`。
