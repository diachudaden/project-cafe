import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import { EmptyFeature } from "@chonrieng/ui";
import { AuthProvider, RequireAuth } from "@chonrieng/auth";
import { apiBaseUrl } from "./config";
import { SignInPage } from "./features/auth/SignInPage";
import { PosLayout } from "./features/navigation/PosLayout";
import { SeatMapPage } from "./features/seats/SeatMapPage";
import "./styles.css";
import "@chonrieng/ui/styles.css";

function UnavailablePage() {
  return (
    <main className="cr-page-content pos-page-content">
      <EmptyFeature title="Trang này" />
    </main>
  );
}

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <AuthProvider apiBaseUrl={apiBaseUrl} client="pos">
      <BrowserRouter>
        <Routes>
          <Route path="/login" element={<SignInPage />} />
          <Route element={<RequireAuth />}>
            <Route element={<PosLayout />}>
              <Route index element={<Navigate to="/seats" replace />} />
              <Route path="seats" element={<SeatMapPage />} />
              <Route path="*" element={<UnavailablePage />} />
            </Route>
          </Route>
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  </StrictMode>,
);
