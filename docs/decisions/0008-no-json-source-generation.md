# 0008 不使用 JSON source generation

- **狀態**：採用（2026-10）
- **決定**：System.Text.Json 維持反射式序列化，不建立 `JsonSerializerContext`。

## 原因

- 不是 Native AOT 部署，source generation 對啟動與吞吐的效益很小。
- 需要維護約 300 個 DTO 的清單，新增 DTO 忘了登記會在執行期才發現。
- 端點委派已由 Request Delegate Generator 在編譯期產生，測試 host 的啟動成本已經處理（見 [測試](../development/TESTING.md#host-啟動為何便宜)）。

## 何時重新評估

改用 Native AOT 或 trimming 時一起做。
