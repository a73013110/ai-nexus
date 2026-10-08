export const permissionFlowchart = `flowchart TD
    classDef process fill:#e1f5fe,stroke:#01579b,stroke-width:2px;
    classDef decision fill:#fff9c4,stroke:#fbc02d,stroke-width:2px;
    classDef error fill:#ffebee,stroke:#c62828,stroke-width:2px;
    classDef result fill:#e8f5e9,stroke:#2e7d32,stroke-width:2px;
    Start([開始]) --> RPA[RPA: 送出立案請求]
    RPA --> API[KGI_API: 讀參數、做檢查]
    API --> FindCase[找立案單位<br/>用 0000 找 RPA 的部門]
    FindCase --> FindJudge[找核判單位<br/>用 921F 查 VKGI_ABRAICH]
    FindJudge --> SAS[SAS 名單比對<br/>看有沒有命中]
    SAS --> CreateCase[建立案件<br/>把單位寫進案件]
    CreateCase --> ReplyRPA[回覆 RPA<br/>回傳立案成功]
    ReplyRPA --> UserLogin[99903 同仁登入 TMSKGI]
    UserLogin --> CheckList{檢視清單邏輯}
    CheckList -->|規則 1| Rule1[清單只會列出<br/>「核判單位 = 99903」的案件]
    CheckList -->|規則 2| Rule2[覆核清單找的是<br/>「99903 & 覆核人員」]
    Rule1 --> DataCheck{檢查案件資料}
    Rule2 --> DataCheck
    DataCheck -->|ChargeDepNo| Data1[立案單位: 管理部]
    DataCheck -->|JudgeDepNo| Data2[核判單位: 99900]
    DataCheck -->|CustDepNo| Data3[歸屬單位: 99900]
    DataCheck -->|PersonID| Data4[覆核人員: 99900]
    Data1 --> Mismatch{比對結果}
    Data2 --> Mismatch
    Data3 --> Mismatch
    Data4 --> Mismatch
    Mismatch -->|99903 != 99900| Result[看不到案件]
    class RPA,API,FindCase,FindJudge,SAS,CreateCase,ReplyRPA,UserLogin process;
    class CheckList,DataCheck,Mismatch decision;
    class Result error;`;
