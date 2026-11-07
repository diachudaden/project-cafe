import React from "react";
import ReactDOM from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import { QueryClientProvider } from "@tanstack/react-query";
import { App as AntApp, ConfigProvider } from "antd";
import viVN from "antd/locale/vi_VN";
import dayjs from "dayjs";
import "dayjs/locale/vi";
import { AuthProvider, queryClient } from "./api";
import { App } from "./App";

dayjs.locale("vi");

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    {/* Larger controls for a counter tablet (reference: "nút hành động chính to"). */}
    <ConfigProvider locale={viVN} componentSize="large" theme={{ token: { colorPrimary: "#8b5a2b", fontSize: 15 } }}>
      <AntApp>
        <QueryClientProvider client={queryClient}>
          <BrowserRouter basename="/pos">
            <AuthProvider>
              <App />
            </AuthProvider>
          </BrowserRouter>
        </QueryClientProvider>
      </AntApp>
    </ConfigProvider>
  </React.StrictMode>,
);
