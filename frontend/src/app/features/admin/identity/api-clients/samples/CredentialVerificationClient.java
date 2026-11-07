// 呼叫帳密驗證服務確認 AD 帳密（Java 8 以上，JSON 用 Gson 2.8.6 以上）。複製到專案後改 package 即可使用。
// 網址與金鑰放設定檔或環境變數，不寫進程式碼；伺服器憑證保持預設驗證，不要換成全部信任的 TrustManager。
package yourcompany.authentication;

import com.google.gson.Gson;
import com.google.gson.JsonElement;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;
import java.io.IOException;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.io.Reader;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.util.LinkedHashMap;
import java.util.Map;

/** 物件不保存登入狀態，可以整個應用程式共用一個。 */
public final class CredentialVerificationClient {
    private static final Gson GSON = new Gson();
    private static final int CONNECT_TIMEOUT_MS = 5_000;
    private static final int READ_TIMEOUT_MS = 15_000;

    private final URL endpoint;
    private final String apiKey;

    /**
     * @param baseUrl 驗證服務網址（結尾的 / 可省略），例如 https://auth.example/
     * @param apiKey 管理員發給你的系統的金鑰
     */
    public CredentialVerificationClient(String baseUrl, String apiKey) throws IOException {
        if (apiKey == null || apiKey.isEmpty()) throw new IllegalArgumentException("apiKey is required.");
        this.endpoint = new URL(new URL(baseUrl.endsWith("/") ? baseUrl : baseUrl + "/"), "api/v1/credential-verifications");
        this.apiKey = apiKey;
    }

    /**
     * 帳密錯誤與失敗次數過多是使用者造成的結果，以 {@link Result} 回傳；
     * 金鑰無效（401）、AD 連不上（503）這類系統問題丟 {@link CredentialVerificationException}，不要當成密碼錯。
     */
    public Result verify(String account, String password) throws IOException {
        Map<String, String> body = new LinkedHashMap<>();
        body.put("account", account);
        body.put("password", password);
        byte[] payload = GSON.toJson(body).getBytes(StandardCharsets.UTF_8);

        HttpURLConnection connection = (HttpURLConnection) endpoint.openConnection();
        try {
            connection.setRequestMethod("POST");
            connection.setConnectTimeout(CONNECT_TIMEOUT_MS);
            connection.setReadTimeout(READ_TIMEOUT_MS);
            connection.setRequestProperty("Authorization", "Bearer " + apiKey);
            connection.setRequestProperty("Content-Type", "application/json; charset=utf-8");
            connection.setRequestProperty("Accept", "application/json");
            connection.setDoOutput(true);
            try (OutputStream out = connection.getOutputStream()) {
                out.write(payload);
            }

            int status = connection.getResponseCode();
            switch (status) {
                case 200:
                    try (Reader reader = new InputStreamReader(connection.getInputStream(), StandardCharsets.UTF_8)) {
                        return Result.verified(GSON.fromJson(reader, VerifiedUser.class));
                    }
                case 422:
                    return Result.rejected();
                case 429:
                    return Result.rateLimited(retryAfterSeconds(connection.getHeaderField("Retry-After")));
                default:
                    throw new CredentialVerificationException(status, issueCode(connection.getErrorStream()));
            }
        } finally {
            connection.disconnect();
        }
    }

    // Retry-After 是秒數；缺少或格式不對時回 0，由呼叫端自行決定等多久。
    private static int retryAfterSeconds(String header) {
        try {
            return header == null ? 0 : Math.max(0, Integer.parseInt(header.trim()));
        } catch (NumberFormatException e) {
            return 0;
        }
    }

    // 錯誤回應是 problem+json，查證代碼在 issueCode；中間的代理或閘道回 HTML 時讀不到，就不帶代碼。
    private static String issueCode(InputStream stream) {
        if (stream == null) return null;
        try (Reader reader = new InputStreamReader(stream, StandardCharsets.UTF_8)) {
            JsonElement root = JsonParser.parseReader(reader);
            if (!root.isJsonObject()) return null;
            JsonObject problem = root.getAsJsonObject();
            return problem.has("issueCode") && problem.get("issueCode").isJsonPrimitive() ? problem.get("issueCode").getAsString() : null;
        } catch (RuntimeException | IOException e) {
            return null;
        }
    }

    public enum Outcome {
        /** 帳密正確。 */
        VERIFIED,
        /** 帳號或密碼錯，或帳號在 AD 已停用；畫面一律顯示「帳號或密碼不正確」。 */
        REJECTED,
        /** 同一帳號失敗太多次；過了 {@link Result#getRetryAfterSeconds()} 秒再試。 */
        RATE_LIMITED
    }

    public static final class Result {
        private final Outcome outcome;
        private final VerifiedUser user;
        private final int retryAfterSeconds;

        private Result(Outcome outcome, VerifiedUser user, int retryAfterSeconds) {
            this.outcome = outcome;
            this.user = user;
            this.retryAfterSeconds = retryAfterSeconds;
        }

        static Result verified(VerifiedUser user) {
            return new Result(Outcome.VERIFIED, user, 0);
        }

        static Result rejected() {
            return new Result(Outcome.REJECTED, null, 0);
        }

        static Result rateLimited(int retryAfterSeconds) {
            return new Result(Outcome.RATE_LIMITED, null, retryAfterSeconds);
        }

        public Outcome getOutcome() {
            return outcome;
        }

        /** 只有 {@link Outcome#VERIFIED} 才有值。 */
        public VerifiedUser getUser() {
            return user;
        }

        public int getRetryAfterSeconds() {
            return retryAfterSeconds;
        }
    }

    /** 以 sid 對應自己的使用者：帳號改名也不變；email 可能為 null。 */
    public static final class VerifiedUser {
        private String sid;
        private String account;
        private String displayName;
        private String email;

        public String getSid() {
            return sid;
        }

        public String getAccount() {
            return account;
        }

        public String getDisplayName() {
            return displayName;
        }

        public String getEmail() {
            return email;
        }
    }

    /** 系統問題，不是密碼錯；把 {@link #getIssueCode()} 交給驗證服務的管理員查日誌。 */
    public static final class CredentialVerificationException extends IOException {
        private static final long serialVersionUID = 1L;
        private final int status;
        private final String issueCode;

        CredentialVerificationException(int status, String issueCode) {
            super("Credential verification failed with HTTP " + status + (issueCode == null ? "." : " (issue " + issueCode + ")."));
            this.status = status;
            this.issueCode = issueCode;
        }

        public int getStatus() {
            return status;
        }

        public String getIssueCode() {
            return issueCode;
        }
    }
}
