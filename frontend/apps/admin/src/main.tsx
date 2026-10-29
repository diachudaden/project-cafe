import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { BrowserRouter, Route, Routes } from "react-router-dom";
import { apiBaseUrl } from "./config";
import "./styles.css";

function WorkspaceHome() {
  return (
    <main className="workspace">
      <p className="workspace__eyebrow">Chốn Riêng · {__APP_NAME__}</p>
      <h1>Frontend workspace đã sẵn sàng</h1>
      <p>Đây là bộ khung ứng dụng. Các màn hình sẽ được triển khai trong những task tiếp theo.</p>
      <p className="workspace__config">
        API backend: <code>{apiBaseUrl}</code>
      </p>
    </main>
  );
}

function NotFound() {
  return (
    <main className="workspace">
      <h1>Không tìm thấy trang</h1>
      <a href="/">Quay lại trang chính</a>
    </main>
  );
}

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<WorkspaceHome />} />
        <Route path="*" element={<NotFound />} />
      </Routes>
    </BrowserRouter>
  </StrictMode>,
);
