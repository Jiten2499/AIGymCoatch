"use client";

import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import { useQueryClient } from "@tanstack/react-query";
import { useEffect } from "react";
import { apiRoot } from "./api";
import { getSession } from "./session";

export type CircularPublishedEvent = {
  communicationId?: string;
  tenantId?: string;
  publishedAtUtc?: string;
  audience?: string;
};

export function acceptCircularEvent(event: CircularPublishedEvent | null | undefined, tenantId: string | null): boolean {
  return Boolean(tenantId && event?.communicationId && event.tenantId === tenantId);
}

export function useCircularRealtime(tenantId: string | null, accessToken: string | undefined) {
  const client = useQueryClient();
  useEffect(() => {
    if (!tenantId || !accessToken) {
      return undefined;
    }

    const connection = new HubConnectionBuilder()
      .withUrl(`${apiRoot().replace(/\/api\/v1\/?$/, "")}/hubs/chat`, {
        accessTokenFactory: () => getSession()?.accessToken ?? "",
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Error)
      .build();
    const refresh = () => {
      void client.invalidateQueries({ queryKey: ["communications"] });
      void client.invalidateQueries({ queryKey: ["communication-unread"] });
      void client.invalidateQueries({ queryKey: ["communication-inbox"] });
      void client.invalidateQueries({ queryKey: ["communication-inbox-item"] });
    };
    const onPublished = (event: CircularPublishedEvent) => {
      if (acceptCircularEvent(event, getSession()?.currentTenant?.id ?? tenantId)) {
        refresh();
      }
    };
    connection.on("CircularPublished", onPublished);
    connection.onreconnected(refresh);
    void connection.start().catch(() => undefined);
    return () => {
      void connection.stop();
    };
  }, [accessToken, client, tenantId]);
}
