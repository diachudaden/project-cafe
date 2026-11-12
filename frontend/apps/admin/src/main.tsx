import React from "react";
import ReactDOM from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import { QueryClientProvider } from "@tanstack/react-query";
import { App as AntApp } from "antd";
import { AppThemeProvider } from "@cafecr/ui";
import viVN from "antd/locale/vi_VN";
import dayjs from "dayjs";
import "dayjs/locale/vi";
import customParseFormat from "dayjs/plugin/customParseFormat";
import { AuthProvider, queryClient } from "./api";
import { BranchProvider } from "./branch/BranchContext";
import { App } from "./App";

dayjs.extend(customParseFormat);
dayjs.locale("vi");

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <AppThemeProvider locale={viVN} token={{ colorPrimary: "#8b5a2b" }}>
      <AntApp>
        <QueryClientProvider client={queryClient}>
          <BrowserRouter basename="/admin">
            <AuthProvider>
              <BranchProvider>
                <App />
              </BranchProvider>
            </AuthProvider>
          </BrowserRouter>
        </QueryClientProvider>
      </AntApp>
    </AppThemeProvider>
  </React.StrictMode>,
);
