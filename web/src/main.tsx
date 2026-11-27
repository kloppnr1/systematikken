import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { BrowserRouter, Route, Routes } from "react-router";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ApiError } from "./api";
import { Layout } from "./Layout";
import { ToastProvider } from "./ui";
import "@fontsource-variable/manrope";
import "./styles.css";

import { Login } from "./pages/Login";
import { Start, InvitationPage } from "./pages/Start";
import { Todo } from "./pages/Todo";
import { Search } from "./pages/Search";
import { Children } from "./pages/Children";
import { NewChild } from "./pages/NewChild";
import { ChildPage } from "./pages/Child";
import { WaitingList } from "./pages/WaitingList";
import { ApplicantPage } from "./pages/Applicant";
import { Billing } from "./pages/Billing";
import { BillingRun } from "./pages/BillingRun";
import { Payments } from "./pages/Payments";
import { Occupancy } from "./pages/Occupancy";
import { Settings } from "./pages/Settings";
import { More } from "./pages/More";
import { Signup, Thanks } from "./pages/public/Signup";
import { Answer } from "./pages/public/Answer";
import { NotFound } from "./pages/NotFound";

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: (count, error) => !(error instanceof ApiError && error.status < 500) && count < 2,
      refetchOnWindowFocus: true,
    },
  },
});

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <ToastProvider>
        <BrowserRouter>
          <Routes>
            <Route path="/login" element={<Login />} />
            <Route path="/start" element={<Start />} />
            <Route path="/invitation/:token" element={<InvitationPage />} />
            <Route path="/tilmeld/:slug" element={<Signup />} />
            <Route path="/tilmeld/:slug/tak" element={<Thanks />} />
            <Route path="/svar/:token" element={<Answer />} />
            <Route element={<Layout />}>
              <Route index element={<Todo />} />
              <Route path="/soeg" element={<Search />} />
              <Route path="/born" element={<Children />} />
              <Route path="/born/nyt" element={<NewChild />} />
              <Route path="/born/:id" element={<ChildPage />} />
              <Route path="/venteliste" element={<WaitingList />} />
              <Route path="/venteliste/:id" element={<ApplicantPage />} />
              <Route path="/opkraevning" element={<Billing />} />
              <Route path="/opkraevning/betalinger" element={<Payments />} />
              <Route path="/opkraevning/:id" element={<BillingRun />} />
              <Route path="/pladser" element={<Occupancy />} />
              <Route path="/indstillinger" element={<Settings />} />
              <Route path="/mere" element={<More />} />
              <Route path="*" element={<NotFound />} />
            </Route>
          </Routes>
        </BrowserRouter>
      </ToastProvider>
    </QueryClientProvider>
  </StrictMode>,
);
