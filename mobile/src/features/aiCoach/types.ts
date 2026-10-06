export type ConversationSummary = {
  id: string;
  title: string;
  updatedAt: string;
};

export type ConversationMessage = {
  role: 'User' | 'Assistant';
  content: string;
  createdAt: string;
};

export type ConversationDetail = {
  id: string;
  title: string;
  messages: ConversationMessage[];
};
