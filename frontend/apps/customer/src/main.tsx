import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import { AuthProvider, RequireAuth } from "@chonrieng/auth";
import { apiBaseUrl } from "./config";
import { SignInPage } from "./features/auth/SignInPage";
import { CustomerLayout } from "./features/navigation/CustomerLayout";
import { PointsPage } from "./features/loyalty/PointsPage";
import { PlacesPage } from "./features/places/PlacesPage";
import { ProfilePage } from "./features/profile/ProfilePage";
import { ScanPage } from "./features/scan/ScanPage";
import "./styles.css";
import "@chonrieng/ui/styles.css";

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <AuthProvider apiBaseUrl={apiBaseUrl} client="customer">
      <BrowserRouter>
        <Routes>
          <Route path="/login" element={<SignInPage />} />
          <Route element={<RequireAuth />}>
            <Route element={<CustomerLayout />}>
              <Route index element={<Navigate to="/places" replace />} />
              <Route path="places" element={<PlacesPage />} />
              <Route path="scan" element={<ScanPage />} />
              <Route path="points" element={<PointsPage />} />
              <Route path="profile" element={<ProfilePage />} />
              <Route path="*" element={<Navigate to="/places" replace />} />
            </Route>
          </Route>
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  </StrictMode>,
);
