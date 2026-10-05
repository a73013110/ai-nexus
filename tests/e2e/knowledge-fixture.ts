import { randomUUID } from "node:crypto";
import type { Page } from "@playwright/test";
import { ApiFixture } from "./fixtures";
import type {
  Collection,
  DocumentInfo,
  DocumentPage,
  Job,
  ResourceAcl,
} from "../../frontend/src/app/core/api/types";

// Synthetic pages only: no company files, network provider calls or production authentication.
export function twoPagePdf() {
  const objects = [
    "<< /Type /Catalog /Pages 2 0 R >>",
    "<< /Type /Pages /Kids [3 0 R 4 0 R] /Count 2 >>",
    "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 400 500] /Resources << /Font << /F1 5 0 R >> >> /Contents 6 0 R >>",
    "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 400 500] /Resources << /Font << /F1 5 0 R >> >> /Contents 7 0 R >>",
    "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
    ...["Travel policy - first page", "Approval signature - second page"].map(
      (text) => {
        const stream = `BT /F1 18 Tf 30 430 Td (${text}) Tj ET`;
        return `<< /Length ${stream.length} >>\nstream\n${stream}\nendstream`;
      },
    ),
  ];
  let pdf = "%PDF-1.4\n";
  const offsets = [0];
  objects.forEach((object, i) => {
    offsets.push(Buffer.byteLength(pdf));
    pdf += `${i + 1} 0 obj\n${object}\nendobj\n`;
  });
  const xref = Buffer.byteLength(pdf);
  pdf +=
    `xref\n0 ${objects.length + 1}\n0000000000 65535 f \n` +
    offsets
      .slice(1)
      .map((x) => `${String(x).padStart(10, "0")} 00000 n \n`)
      .join("");
  pdf += `trailer\n<< /Size ${objects.length + 1} /Root 1 0 R >>\nstartxref\n${xref}\n%%EOF`;
  return Buffer.from(pdf);
}
export class KnowledgeFixture {
  readonly core = new ApiFixture();
  readonly collections: Collection[] = [];
  readonly documents: DocumentInfo[] = [];
  readonly attachmentDocuments = new Map<string, string>();
  readonly documentFiles = new Map<string, string>();
  readonly jobs: Job[] = [];
  readonly selections = new Map<string, string[]>();
  acl: ResourceAcl = { members: [], groupIds: [] };
  readonly colleague = {
    id: randomUUID(),
    account: "TEST\\colleague",
    displayName: "林同事",
  };
  sourceQueries = 0;
  seed() {
    const collection = this.collection("公司作業規範");
    const id = randomUUID(),
      jobId = randomUUID();
    this.documents.push({
      id,
      collectionId: collection.resource.id,
      fileName: "差旅費用申請.pdf",
      contentType: "application/pdf",
      status: "ready",
      pageCount: 2,
      chunkCount: 2,
      warning: null,
      jobId,
      canEdit: true,
      hasOriginal: true,
    });
    this.jobs.push({
      id: jobId,
      subjectId: id,
      kind: "document-ingest",
      label: "差旅費用申請.pdf",
      status: "completed",
      stage: "處理完成",
      completedUnits: 2,
      totalUnits: 2,
      attempt: 1,
      cancelRequested: false,
      errorCode: null,
      errorMessage: null,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    });
    collection.documents = 1;
    collection.readyDocuments = 1;
    const fileId = randomUUID();
    this.core.attachments.push({
      id: fileId,
      fileName: "差旅費用申請.pdf",
      contentType: "application/pdf",
      size: twoPagePdf().length,
      isImage: false,
      analysisMode: "extracted-text",
    });
    this.core.attachmentData.set(fileId, twoPagePdf());
    this.core.retainedFiles.add(fileId);
    this.core.fileUsages.set(fileId, [
      {
        kind: "knowledge",
        resourceId: collection.resource.id,
        name: collection.resource.name,
        documentId: id,
        status: "ready",
      },
    ]);
    this.documentFiles.set(id, fileId);
    return this.documents[0];
  }
  collection(name: string) {
    const value: Collection = {
      resource: {
        id: randomUUID(),
        name,
        kind: "knowledge",
        canEdit: true,
        isOwner: true,
        updatedAt: new Date().toISOString(),
      },
      description: "同事可共用的文件來源，引用內容直接回到原檔核對。",
      documents: 0,
      readyDocuments: 0,
    };
    this.collections.push(value);
    return value;
  }
  async attach(page: Page) {
    this.core.extraFeatures = [
      { id: "knowledge", name: "知識庫", route: "/knowledge" },
      { id: "tasks", name: "背景任務", route: "/tasks" },
    ];
    await this.core.attach(page);
    await page.route("**/api/v1/**", async (route) => {
      const path = new URL(route.request().url()).pathname.replace(
          "/api/v1",
          "",
        ),
        method = route.request().method();
      const json = (body: unknown, status = 200) =>
        route.fulfill({
          status,
          contentType: "application/json",
          body: JSON.stringify(body),
        });
      if (path === "/knowledge/collections") {
        if (method === "POST") {
          const request = route.request().postDataJSON();
          return json(this.collection(request.name));
        }
        return json(this.collections);
      }
      const selected = /^\/conversations\/([^/]+)\/knowledge$/.exec(path);
      if (selected) {
        if (method === "PUT") {
          this.selections.set(
            selected[1],
            route.request().postDataJSON().collectionIds,
          );
          return route.fulfill({ status: 204 });
        }
        return json({ collectionIds: this.selections.get(selected[1]) || [] });
      }
      const list = /^\/knowledge\/collections\/([^/]+)\/documents$/.exec(path);
      if (list) {
        if (method === "POST") {
          const file = this.core.attachments.find(
            (x) => x.id === route.request().postDataJSON().attachmentId,
          )!;
          const document: DocumentInfo = {
            id: randomUUID(),
            collectionId: list[1],
            fileName: file.fileName,
            contentType: file.contentType,
            status: "ready",
            pageCount: 1,
            chunkCount: 1,
            warning: null,
            jobId: null,
            canEdit: true,
            hasOriginal: true,
          };
          this.documents.push(document);
          this.documentFiles.set(document.id, file.id);
          this.core.retainedFiles.add(file.id);
          const collection = this.collections.find(
            (value) => value.resource.id === list[1],
          )!;
          const usages = this.core.fileUsages.get(file.id) || [];
          usages.push({
            kind: "knowledge",
            resourceId: list[1],
            name: collection.resource.name,
            documentId: document.id,
            status: "ready",
          });
          this.core.fileUsages.set(file.id, usages);
          return json(document);
        }
        return json(this.documents.filter((x) => x.collectionId === list[1]));
      }
      if (/\/knowledge\/collections\/[^/]+\/access$/.test(path)) {
        if (method === "PUT") {
          this.acl = {
            ...route.request().postDataJSON(),
            members: route
              .request()
              .postDataJSON()
              .members.map((x: { userId: string; role: string }) => ({
                ...x,
                account: this.colleague.account,
                displayName: this.colleague.displayName,
              })),
          };
          return route.fulfill({ status: 204 });
        }
        return json(this.acl);
      }
      if (path === "/directory")
        return json([
          {
            id: this.core.userId,
            account: "TEST\\fixture",
            displayName: "測試使用者",
          },
          this.colleague,
        ]);
      if (path === "/directory/groups")
        return json([{ id: "workspace", name: "基本工作區" }]);
      if (path === "/knowledge/search") {
        this.sourceQueries++;
        const doc = this.documents[0];
        return json({
          mode: "semantic",
          hits: doc
            ? [
                {
                  documentId: doc.id,
                  title: doc.fileName,
                  pageNumber: 2,
                  text: "差旅申請需主管簽核。",
                  score: 0.8,
                },
              ]
            : [],
        });
      }
      const attachmentDocument = /^\/attachments\/([^/]+)\/document$/.exec(
        path,
      );
      if (attachmentDocument) {
        const file = this.core.attachments.find(
          (value) => value.id === attachmentDocument[1],
        );
        if (!file) return json({ title: "找不到附件。" }, 404);
        const existing = this.attachmentDocuments.get(file.id);
        if (existing)
          return json(this.documents.find((value) => value.id === existing));
        const doc: DocumentInfo = {
          id: randomUUID(),
          collectionId: null,
          fileName: file.fileName,
          contentType: file.contentType,
          status: "ready",
          pageCount: 1,
          chunkCount: 1,
          warning: file.isImage
            ? "包含 AI 辨識文字，使用前請對照原始頁面確認。"
            : null,
          jobId: null,
          canEdit: true,
          hasOriginal: true,
        };
        this.documents.push(doc);
        this.attachmentDocuments.set(file.id, doc.id);
        return json(doc);
      }
      const document = /^\/documents\/([^/]+)(\/pages|\/content|\/job)?$/.exec(
        path,
      );
      if (document) {
        const doc = this.documents.find((x) => x.id === document[1]);
        if (!doc) return json({ title: "來源已移除。" }, 404);
        if (method === "DELETE") {
          const file = this.documentFiles.get(doc.id);
          if (file)
            this.core.fileUsages.set(
              file,
              (this.core.fileUsages.get(file) || []).filter(
                (value) => value.documentId !== doc.id,
              ),
            );
          this.documents.splice(this.documents.indexOf(doc), 1);
          return route.fulfill({ status: 204 });
        }
        if (document[2] === "/pages" && doc.contentType.startsWith("image/"))
          return json([
            {
              pageNumber: 1,
              text: "圖片內容：文件、索引與答案的工作流程。",
              extraction: "ocr",
              needsReview: true,
            },
          ]);
        if (
          document[2] === "/content" &&
          doc.contentType.startsWith("image/")
        ) {
          const source = Array.from(this.attachmentDocuments).find(
            ([, id]) => id === doc.id,
          )?.[0];
          return route.fulfill({
            contentType: doc.contentType,
            body: this.core.attachmentData.get(source!),
          });
        }
        if (document[2] === "/pages")
          return json([
            {
              pageNumber: 1,
              text: "第一頁：差旅費用申請說明。",
              extraction: "native",
              needsReview: false,
            },
            {
              pageNumber: 2,
              text: "第二頁：差旅申請需主管簽核。<img src=x onerror=alert(1)>",
              extraction: "ocr",
              needsReview: true,
            },
          ] satisfies DocumentPage[]);
        if (document[2] === "/content")
          return route.fulfill({
            contentType: "application/pdf",
            body: twoPagePdf(),
          });
        if (document[2] === "/job")
          return json({
            job: this.jobs.find((x) => x.id === doc.jobId),
            canControl: true,
          });
        return json(doc);
      }
      if (path === "/jobs") return json(this.jobs);
      const job = /^\/jobs\/([^/]+)\/(cancel|retry)$/.exec(path);
      if (job) {
        const value = this.jobs.find((x) => x.id === job[1])!;
        if (job[2] === "cancel") value.cancelRequested = true;
        else {
          value.status = "queued";
          value.errorCode = null;
          value.errorMessage = null;
          value.stage = "等待處理";
        }
        return json(value);
      }
      return route.fallback();
    });
  }
}
