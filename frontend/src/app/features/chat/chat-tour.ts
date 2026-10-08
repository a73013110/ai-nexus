import type { ProductTourDefinition } from '../../shared/ui/product-tour';

export function chatTour(narrow: boolean, knowledge: boolean): ProductTourDefinition {
  return {
    steps: [
      {
        target: narrow ? '.workspace-menu-button' : '.workspace-sidebar .new-chat',
        title: '從這裡開始新的思路',
        description: narrow
          ? '點選左上角展開導覽，開始新對話、尋找歷史紀錄或切換工作區。收合後，整個畫面留給你的內容。'
          : '開始新對話，保留每件工作的脈絡。側邊欄也能搜尋歷史、開啟常用範本與切換工作區。',
        side: 'right',
      },
      {
        target: '.composer-input',
        title: '把問題交給 AI',
        description:
          '描述目標、補充背景與期待的格式。下方會顯示目前的送出與換行快捷鍵；右上角可展開輸入區。',
      },
      {
        target: '.composer .attach-button',
        title: '帶上你的文件與圖片',
        description: '點選迴紋針加入附件，也可以拖放檔案或貼上圖片。AI 會依附件內容協助分析。',
      },
      {
        target: 'nx-composer-controls',
        title: '選擇適合的回答方式',
        description:
          '在這裡選擇模型、推理強度與可用的網路搜尋。切換前可查看模型能力與 Context 使用量。',
      },
      ...(knowledge
        ? [
            {
              target: 'nx-knowledge-picker',
              title: '讓回答有資料依據',
              description:
                '加入知識庫來源，讓回答參考你的文件。需要查證時，可從回答的來源開啟閱讀器。',
            },
          ]
        : []),
      {
        target: '.composer .send-button',
        title: '送出，然後一起完善',
        description:
          '準備好就送出訊息。回答後可繼續追問、複製內容、重新生成或編輯提問建立新分支。上方「操作導覽」的問號圖示可隨時重播導覽。',
      },
    ],
  };
}
