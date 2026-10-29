import { Navigate, useNavigate } from "react-router-dom";
import { SignInForm, StatePanel, type SignInValues } from "@chonrieng/ui";
import { useAuth } from "@chonrieng/auth";

export function SignInPage() {
  const { error, fieldErrors, signIn, status } = useAuth();
  const navigate = useNavigate();

  if (status === "checking") {
    return <main className="cr-auth-gate"><StatePanel state="loading" title="Đang kiểm tra phiên đăng nhập" /></main>;
  }

  if (status === "authenticated") {
    return <Navigate to="/seats" replace />;
  }

  const submit = async (values: SignInValues) => {
    if (await signIn(values)) {
      navigate("/seats", { replace: true });
    }
  };

  return <SignInForm appName="Điểm phục vụ" error={error} fieldErrors={fieldErrors} onSubmit={submit} />;
}
