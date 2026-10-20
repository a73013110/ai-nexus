# 帳號安全：通知信箱與登入方式

## 帳號與通知信箱

- `Account` 是完整帳號（AD 為 `sam@網域`，Windows 模式為 `DOMAIN\sam`），用於顯示、搜尋與外部來源授權，**不是 email**；改名只有成本，所以保留。
- AD 表單登入只用帳號名（`sAMAccountName`）。輸入 `DOMAIN\chen`、`chen@<AD 網域>` 或 `Identity:ActiveDirectory:AccountSuffixes` 列出的後綴時會自動去掉；其他 `@` 輸入與帳密錯誤同一回應。
- 通知信箱 `Users.Email` 只用來寄驗證碼與帳號安全通知，不是登入識別、不唯一（共用信箱、別名）。來源 `EmailSource`：

| 來源 | 寫入時機 | 本人可改 |
| --- | --- | --- |
| `directory` | AD／Windows 登入時讀目錄 `mail`，寫入即視為已驗證；目錄移除 `mail` 時一併清空 | 否 |
| `user` | 本人以驗證碼驗證新地址 | 是 |
| `admin` | 保留給管理員設定（目前管理端未提供）；本人驗證前視為未驗證 | 是 |

- 目錄只覆寫「沒有信箱」或「來源是 `directory`」的值；`ProfileManaged=1`（管理員維護個人資料）的使用者姓名與信箱都不被目錄覆寫。
- 每次 AD 驗證成功（LDAP 登入、Windows 驗證的請求，至多每 5 分鐘寫一次）更新 `DirectoryVerifiedAt`，供本地密碼新鮮度規則使用。
