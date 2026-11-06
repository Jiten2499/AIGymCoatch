"use client";

import { GoogleDriveSection } from "./GoogleDriveSection";

export default function IntegrationsPage() {
  return (
    <div>
      <h1 className="text-3xl font-semibold">Integrations</h1>
      <p className="mt-2 max-w-3xl text-muted">Connect external storage used by LifeFit for member progress photos.</p>
      <div className="mt-8">
        <GoogleDriveSection />
      </div>
    </div>
  );
}
