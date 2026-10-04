const failures: Readonly<Record<string, string>> = {
  generation_timeout: '模型回應逾時，已保留收到的內容',
  server_restarted: '伺服器重新啟動，生成已中斷',
  server_stopping: '伺服器正在關閉，生成已中斷',
  executor_lost: '執行此回答的服務已中斷，已保留收到的內容',
  orphaned_run: '執行此回答的服務已中斷，已保留收到的內容',
  provider_connection_lost: '與模型服務的連線中斷，已保留收到的內容',
  provider_stream_incomplete: '模型串流提前結束，已保留收到的內容',
  provider_protocol_error: '模型回應格式不正確',
  provider_frame_too_large: '模型回傳的單筆資料超過限制，請分段提問',
  output_limit_exceeded: '回答超過文字上限，請分段提問',
  google_quota_exceeded: '模型服務的額度或速率受限，請稍後重試',
  google_key_rejected: '模型服務的憑證無效，請聯絡管理員',
  google_api_key_missing: '尚未設定模型服務的憑證，請聯絡管理員',
  google_model_unavailable: '模型目前無法使用，請聯絡管理員',
  google_service_unavailable: '模型服務暫時無法使用，請稍後重試',
  google_response_blocked: '模型未完成此回答，請調整提問',
  google_stream_error: '模型串流發生錯誤，已保留收到的內容',
  model_access_denied: '目前模型的使用權限已變更',
  chat_access_revoked: '對話功能的使用權限已撤銷',
};

export function generationError(code: string | null | undefined): string {
  return failures[code ?? ''] ?? '生成未完成，已保留收到的內容';
}
