"use client";

import { ProfilePhotoCard } from "../../../components/ProfilePhotoCard";

export default function AdminProfilePage() {
  return (
    <div>
      <h1 className="text-3xl font-semibold">Profile</h1>
      <p className="mt-2 max-w-3xl text-muted">Update the picture shown for your platform account.</p>
      <div className="mt-8">
        <ProfilePhotoCard />
      </div>
    </div>
  );
}
