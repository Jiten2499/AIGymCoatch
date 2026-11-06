"use client";

import { useEffect } from "react";
import { refreshSession } from "../lib/api";
import { getSession, hasActivity, isIdle, setSession, touchActivity } from "../lib/session";

const renewLeadMs = 2 * 60 * 1000;

export function SessionKeeper() {
  useEffect(() => {
    const mark = () => touchActivity();
    const events = ["pointerdown", "keydown", "scroll"] as const;
    events.forEach((name) => window.addEventListener(name, mark, { passive: true }));

    const tick = () => {
      const session = getSession();
      if (!session) {
        return;
      }

      if (isIdle()) {
        setSession(null);
        return;
      }

      const expires = Date.parse(session.expiresAt);
      if (!Number.isNaN(expires) && expires - Date.now() < renewLeadMs) {
        void refreshSession();
      }
    };

    if (getSession() && !hasActivity()) {
      touchActivity();
    }

    tick();
    const timer = window.setInterval(tick, 60_000);
    return () => {
      events.forEach((name) => window.removeEventListener(name, mark));
      window.clearInterval(timer);
    };
  }, []);

  return null;
}
