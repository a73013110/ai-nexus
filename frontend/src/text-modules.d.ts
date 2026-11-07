/** 以文字匯入的原始檔（angular.json 的 loader）：發給其他系統的程式碼範例，原檔也由後端測試或編譯檢查。 */
declare module '*.cs' {
  const source: string;
  export default source;
}
declare module '*.java' {
  const source: string;
  export default source;
}
