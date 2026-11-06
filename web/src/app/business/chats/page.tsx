"use client";

import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { api, apiRoot, ApiError } from "../../../lib/api";
import { getSession } from "../../../lib/session";
import { ChatAvatar } from "./ChatAvatar";

type ChatRole = "Member" | "Trainer" | "Staff" | "Admin";
type InboxItem = { conversationId: string; otherUserId: string; displayName: string; role: ChatRole; lastPreview: string | null; lastMessageAt: string | null; unreadCount: number; hasPhoto?: boolean };
type ChatMessage = { id: string; senderUserId: string; messageType: string; body: string; createdAt: string; isReadByOther: boolean; attachmentId: string | null; fileName: string | null };
type Person = { userId: string; displayName: string; role: ChatRole; hasPhoto?: boolean };

const filters = ["", "unread", "trainers", "members"] as const;

export default function ChatsPage() {
  const client = useQueryClient();
  const [filter, setFilter] = useState<(typeof filters)[number]>("");
  const [term, setTerm] = useState("");
  const [selected, setSelected] = useState<InboxItem | null>(null);
  const [draft, setDraft] = useState("");
  const [typing, setTyping] = useState(false);
  const [finding, setFinding] = useState(false);
  const features = useQuery({
    queryKey: ["features", getSession()?.currentTenant?.id ?? null],
    queryFn: () => api<{ featureCode: string; enabled: boolean }[]>("/features"),
  });
  const chatEnabled = features.data?.some((feature) => feature.featureCode === "GYM_CHAT" && feature.enabled) === true;
  const inbox = useQuery({
    queryKey: ["chat-inbox", filter, term],
    enabled: chatEnabled,
    queryFn: () => api<InboxItem[]>(`/chat/conversations?take=30&filter=${filter}&term=${encodeURIComponent(term)}`),
  });
  const messages = useQuery({
    queryKey: ["chat-messages", selected?.conversationId],
    enabled: chatEnabled && Boolean(selected),
    queryFn: () => api<ChatMessage[]>(`/chat/conversations/${selected?.conversationId}/messages?take=40`),
  });
  const directoryOpen = finding || filter === "trainers" || filter === "members";
  const people = useQuery({
    queryKey: ["chat-recipients", term],
    enabled: chatEnabled && directoryOpen,
    queryFn: () => api<Person[]>(`/chat/recipients?term=${encodeURIComponent(term)}`),
  });
  const open = useMutation({
    mutationFn: (recipientUserId: string) => api<InboxItem>("/chat/conversations", { method: "POST", body: JSON.stringify({ recipientUserId }) }),
    onSuccess: async (conversation) => {
      setFinding(false);
      setSelected(conversation);
      await client.invalidateQueries({ queryKey: ["chat-inbox"] });
    },
  });
  const send = useMutation({
    mutationFn: (body: string) => api<ChatMessage>(`/chat/conversations/${selected?.conversationId}/messages`, { method: "POST", body: JSON.stringify({ body, clientMessageId: `${Date.now()}` }) }),
    onSuccess: async () => {
      setDraft("");
      await client.invalidateQueries({ queryKey: ["chat-messages", selected?.conversationId] });
      await client.invalidateQueries({ queryKey: ["chat-inbox"] });
    },
  });
  const upload = useMutation({
    mutationFn: async (file: File) => {
      const body = new FormData();
      body.append("file", file);
      body.append("clientMessageId", `${Date.now()}-file`);
      return api<ChatMessage>(`/chat/conversations/${selected?.conversationId}/attachments`, { method: "POST", body });
    },
    onSuccess: async () => {
      await client.invalidateQueries({ queryKey: ["chat-messages", selected?.conversationId] });
      await client.invalidateQueries({ queryKey: ["chat-inbox"] });
    },
  });

  useEffect(() => {
    const session = getSession();
    if (!session || !chatEnabled) {
      return undefined;
    }
    const connection = new HubConnectionBuilder()
      .withUrl(`${apiRoot().replace(/\/api\/v1\/?$/, "")}/hubs/chat`, { accessTokenFactory: () => getSession()?.accessToken ?? "" })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Error)
      .build();
    const refresh = () => {
      void client.invalidateQueries({ queryKey: ["chat-inbox"] });
      void client.invalidateQueries({ queryKey: ["chat-messages"] });
    };
    connection.on("MessageReceived", refresh);
    connection.on("ConversationUpdated", refresh);
    connection.on("MessageRead", refresh);
    connection.on("TypingStarted", () => setTyping(true));
    connection.on("TypingStopped", () => setTyping(false));
    void connection.start().then(() => {
      if (selected) {
        return connection.invoke("JoinConversation", selected.conversationId);
      }
      return undefined;
    }).catch(() => undefined);
    return () => {
      void connection.stop();
    };
  }, [chatEnabled, client, selected]);

  useEffect(() => {
    const latest = messages.data?.find((item) => item.senderUserId !== getSession()?.user.id);
    if (selected && latest) {
      void api(`/chat/conversations/${selected.conversationId}/read`, { method: "POST", body: JSON.stringify({ messageId: latest.id }) });
    }
  }, [messages.data, selected]);

  const thread = [...(messages.data ?? [])].reverse();
  const me = getSession()?.user.id;
  const visiblePeople = (people.data ?? []).filter((person) => filter === "trainers" ? person.role === "Trainer" : filter === "members" ? person.role === "Member" : true);

  if (features.isSuccess && !chatEnabled) {
    return (
      <div>
        <h1 className="text-3xl font-semibold">Chats</h1>
        <p className="mt-2 text-muted">Chat is turned off for this workspace plan.</p>
      </div>
    );
  }

  return (
    <div>
      <h1 className="text-3xl font-semibold">Chats</h1>
      <p className="mt-2 text-muted">Message members, trainers, and staff you are allowed to reach. Private conversations you are not part of stay private.</p>
      <div className="mt-6 grid min-h-[640px] gap-4 lg:grid-cols-[280px_minmax(0,1fr)_260px]">
        <aside className="rounded-3xl border border-line p-4">
          <input aria-label="Search chats" className="min-h-11 w-full rounded-full border border-line px-4" onChange={(event) => setTerm(event.target.value)} placeholder="Search chats..." value={term} />
          <div className="mt-3 flex flex-wrap gap-2">
            {filters.map((value) => (
              <button className={`min-h-11 rounded-full px-3 ${filter === value ? "bg-accent text-background" : "border border-line"}`} key={value || "all"} onClick={() => setFilter(value)} type="button">{value || "All"}</button>
            ))}
          </div>
          <button className="mt-3 min-h-11 rounded-full border border-line px-3" onClick={() => setFinding((current) => !current)} type="button">New chat</button>
          {directoryOpen && people.isSuccess && visiblePeople.length === 0 ? <p className="mt-3 text-sm text-muted">No people match this chat.</p> : null}
          {directoryOpen ? visiblePeople.map((person) => (
            <button className="mt-3 flex w-full items-center gap-3 text-left" key={person.userId} onClick={() => open.mutate(person.userId)} type="button">
              <ChatAvatar hasPhoto={person.hasPhoto} name={person.displayName} userId={person.userId} />
              <span className="min-w-0">
                <span className="block font-semibold">{person.displayName}</span>
                <span className="block text-sm text-muted">{person.role}</span>
              </span>
            </button>
          )) : null}
          {(inbox.data ?? []).map((item) => (
            <button className={`mt-3 flex w-full items-center gap-3 rounded-2xl px-2 py-2 text-left ${selected?.conversationId === item.conversationId ? "bg-accent/10" : ""}`} key={item.conversationId} onClick={() => setSelected(item)} type="button">
              <ChatAvatar hasPhoto={item.hasPhoto} name={item.displayName} userId={item.otherUserId} />
              <span className="min-w-0">
                <span className="block font-semibold">{item.displayName}{item.unreadCount > 0 ? ` (${item.unreadCount})` : ""}</span>
                <span className="block text-sm text-muted">{item.role}</span>
                <span className="block truncate text-sm">{item.lastPreview}</span>
              </span>
            </button>
          ))}
          {inbox.error instanceof ApiError ? <p className="mt-3 text-red-700">{inbox.error.message}</p> : null}
        </aside>
        <section className="flex flex-col rounded-3xl border border-line">
          {selected ? (
            <>
              <header className="flex items-center gap-3 border-b border-line p-4">
                <ChatAvatar hasPhoto={selected.hasPhoto} name={selected.displayName} userId={selected.otherUserId} />
                <div>
                  <h2 className="text-xl font-semibold">{selected.displayName}</h2>
                  <p className="text-sm text-muted">{selected.role}{typing ? " · Typing..." : ""}</p>
                </div>
              </header>
              <div className="flex flex-1 flex-col gap-3 p-4">
                {thread.map((item) => {
                  const mine = item.senderUserId === me;
                  return (
                    <div className={`max-w-[75%] rounded-2xl px-4 py-2 ${mine ? "self-end bg-accent text-background" : "self-start bg-line/40"}`} key={item.id}>
                      <p>{item.body}</p>
                      <p className="text-xs opacity-80">{new Date(item.createdAt).toLocaleTimeString()}{mine && item.isReadByOther ? " · Read" : ""}{item.fileName ? ` · ${item.fileName}` : ""}</p>
                    </div>
                  );
                })}
              </div>
              <form className="flex gap-2 border-t border-line p-3" onSubmit={(event) => { event.preventDefault(); if (draft.trim()) send.mutate(draft.trim()); }}>
                <input aria-label="Attachment" className="max-w-40" onChange={(event) => { const file = event.target.files?.[0]; if (file) upload.mutate(file); }} type="file" />
                <input aria-label="Message" className="min-h-11 flex-1 rounded-full border border-line px-4" onChange={(event) => setDraft(event.target.value)} placeholder="Type a message..." value={draft} />
                <button className="min-h-11 rounded-full bg-accent px-4 font-semibold text-background" type="submit">Send</button>
              </form>
            </>
          ) : <p className="p-6 text-muted">Select a conversation.</p>}
        </section>
        <aside className="rounded-3xl border border-line p-4">
          <h2 className="font-semibold">Details</h2>
          {selected ? (
            <>
              <div className="mt-4 flex items-center gap-3">
                <ChatAvatar hasPhoto={selected.hasPhoto} name={selected.displayName} userId={selected.otherUserId} />
                <div>
                  <p className="text-lg font-semibold">{selected.displayName}</p>
                  <p className="text-sm text-muted">{selected.role}</p>
                </div>
              </div>
              <p className="mt-4 text-sm">Only people in this conversation can read it.</p>
            </>
          ) : <p className="mt-4 text-sm text-muted">Participant details appear here.</p>}
        </aside>
      </div>
    </div>
  );
}
