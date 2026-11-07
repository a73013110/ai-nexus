// 登入時的用法。網址與金鑰從設定讀取（這裡以環境變數示範），CredentialVerificationClient 建一次共用。
package yourcompany.authentication;

import java.io.IOException;
import java.util.logging.Level;
import java.util.logging.Logger;

final class LoginExample {
    private static final Logger LOG = Logger.getLogger(LoginExample.class.getName());
    private final CredentialVerificationClient credentials;

    LoginExample() throws IOException {
        credentials = new CredentialVerificationClient("{{baseUrl}}", System.getenv("CREDENTIAL_VERIFICATION_API_KEY"));
    }

    /** 回傳要顯示給使用者的訊息；null 代表登入成功。 */
    String login(String account, String password) {
        try {
            CredentialVerificationClient.Result result = credentials.verify(account, password);
            switch (result.getOutcome()) {
                case VERIFIED:
                    signIn(result.getUser());
                    return null;
                case RATE_LIMITED:
                    return "登入失敗次數過多，請 " + result.getRetryAfterSeconds() + " 秒後再試";
                default:
                    return "帳號或密碼不正確";
            }
        } catch (CredentialVerificationClient.CredentialVerificationException e) {
            // 金鑰無效或 AD 連不上，不是密碼錯：記下查證代碼交給驗證服務的管理員。
            LOG.log(Level.SEVERE, "Credential verification unavailable: HTTP {0}, issue {1}", new Object[] { e.getStatus(), e.getIssueCode() });
            return "暫時無法驗證帳密，請聯絡系統管理員";
        } catch (IOException e) {
            LOG.log(Level.SEVERE, "Credential verification unreachable", e);
            return "暫時無法連線到驗證服務，請稍後再試";
        }
    }

    // 以 sid 對應自己的使用者（帳號改名也不變）；能不能登入、有哪些權限由自己的系統決定。
    private void signIn(CredentialVerificationClient.VerifiedUser user) {
    }
}
