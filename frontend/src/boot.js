// 第一幀之前執行：index.html 的 <head> 以同源檔案載入（CSP `script-src 'self'` 不允許 inline）。
// 只做兩件畫面一出現就要成立的事，其餘交給 Angular：
// 1. 套用外觀偏好，深色主題重新載入時不先閃一幀淺色；鍵名與判斷同 ThemeService，它啟動後接手。
// 2. 上一頁換身分時（IdentityVeil.cover）留下的遮罩在這一頁第一幀就蓋上，工作區畫好後由 IdentityVeil 淡出。
(() => {
  const root = document.documentElement;
  let appearance = null;
  try {
    appearance = JSON.parse(localStorage.getItem('ai-nexus.appearance') ?? 'null');
  } catch {
    // 外觀是選用的；儲存可能被政策停用。
  }
  const theme = appearance?.theme;
  root.dataset.theme =
    theme === 'light' || theme === 'dark'
      ? theme
      : matchMedia('(prefers-color-scheme: dark)').matches
        ? 'dark'
        : 'light';
  root.dataset.reducedMotion = String(!!appearance?.reducedMotion);
  try {
    const veil = JSON.parse(sessionStorage.getItem('ai-nexus.identity-veil') ?? 'null');
    sessionStorage.removeItem('ai-nexus.identity-veil');
    // 只接手剛剛的導向；導向失敗後很久才重新整理的頁面不該再蓋遮罩。
    if (typeof veil?.label === 'string' && Date.now() - veil.at < 15000) {
      root.dataset.identityVeil = 'boot';
      root.style.setProperty('--identity-veil-label', JSON.stringify(veil.label));
    }
  } catch {
    // 讀不到就照一般載入，不蓋遮罩。
  }
})();
