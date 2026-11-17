import { createContext, useContext, useEffect, useState, type ReactNode } from "react";
import { Button, ConfigProvider, theme, type ConfigProviderProps, type ThemeConfig } from "antd";

export type ThemeMode = "light" | "dark";

const STORAGE_KEY = "cafecr-theme";

interface ThemeModeContextValue {
  mode: ThemeMode;
  toggle(): void;
}

const ThemeModeContext = createContext<ThemeModeContextValue>({ mode: "light", toggle: () => {} });

function initialMode(): ThemeMode {
  try {
    const saved = localStorage.getItem(STORAGE_KEY);
    if (saved === "light" || saved === "dark") return saved;
  } catch {
    // Storage blocked (private mode): fall back to the OS preference.
  }
  return window.matchMedia?.("(prefers-color-scheme: dark)").matches ? "dark" : "light";
}

/** Paints <body> with the current Ant Design background so non-Layout pages follow the theme too. */
function BodyColors() {
  const { token } = theme.useToken();
  useEffect(() => {
    document.body.style.background = token.colorBgLayout;
    document.body.style.color = token.colorText;
  }, [token.colorBgLayout, token.colorText]);
  return null;
}

type AppThemeProviderProps = Omit<ConfigProviderProps, "theme"> & {
  token?: ThemeConfig["token"];
  children: ReactNode;
};

/**
 * Wraps Ant Design's ConfigProvider with a light/dark switch.
 * The choice is saved per browser; the first visit follows the OS setting.
 */
export function AppThemeProvider({ token, children, ...config }: AppThemeProviderProps) {
  const [mode, setMode] = useState<ThemeMode>(initialMode);

  useEffect(() => {
    document.documentElement.dataset.theme = mode;
    document.documentElement.style.colorScheme = mode;
    try {
      localStorage.setItem(STORAGE_KEY, mode);
    } catch {
      // Ignore: the toggle still works for this tab.
    }
  }, [mode]);

  const toggle = () => setMode((m) => (m === "dark" ? "light" : "dark"));

  return (
    <ThemeModeContext.Provider value={{ mode, toggle }}>
      <ConfigProvider
        {...config}
        theme={{ algorithm: mode === "dark" ? theme.darkAlgorithm : theme.defaultAlgorithm, token }}
      >
        <BodyColors />
        {children}
      </ConfigProvider>
    </ThemeModeContext.Provider>
  );
}

export function useThemeMode() {
  return useContext(ThemeModeContext);
}

/** Small button that flips between light and dark mode. */
export function ThemeToggle({ ghost }: { ghost?: boolean }) {
  const { mode, toggle } = useThemeMode();
  const label = mode === "dark" ? "Chuyển sang giao diện sáng" : "Chuyển sang giao diện tối";
  return (
    <Button ghost={ghost} onClick={toggle} aria-label={label} title={label}>
      {mode === "dark" ? "☀️ Sáng" : "🌙 Tối"}
    </Button>
  );
}
