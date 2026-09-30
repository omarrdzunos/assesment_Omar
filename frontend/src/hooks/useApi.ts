import { useCallback, useEffect, useRef, useState } from "react";
import { ApiError } from "../api/client";

interface ApiState<T> {
  data: T | null;
  loading: boolean;
  error: string | null;
}

// Deliberately not React Query: this app has no shared/cached queries,
// background refetching, or cross-component invalidation to justify it —
// a plain hook covers loading/error state for a handful of GET calls.
export function useApi<T>(fetcher: () => Promise<T>, deps: React.DependencyList = []) {
  const [state, setState] = useState<ApiState<T>>({ data: null, loading: true, error: null });
  const mounted = useRef(true);

  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
    };
  }, []);

  const load = useCallback(() => {
    setState((s) => ({ ...s, loading: true, error: null }));
    fetcher()
      .then((data) => {
        if (mounted.current) setState({ data, loading: false, error: null });
      })
      .catch((err) => {
        if (!mounted.current) return;
        const message = err instanceof ApiError ? err.message : "Something went wrong. Please try again.";
        setState({ data: null, loading: false, error: message });
      });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, deps);

  useEffect(() => {
    load();
  }, [load]);

  return { ...state, refetch: load };
}
