import { useQuery } from "@tanstack/react-query";
import { api } from "./api";

export type Me = {
  signedIn: boolean;
  email?: string;
  name?: string;
  institution?: { id: string; name: string; slug: string } | null;
  invitations?: { token: string; institutionName: string }[];
  providers: { name: string; displayName: string }[];
  devLogin: boolean;
  demo?: boolean;
  operator?: boolean;
};

export const useMe = () => useQuery({ queryKey: ["me"], queryFn: () => api.get<Me>("/api/me"), staleTime: 60_000 });
