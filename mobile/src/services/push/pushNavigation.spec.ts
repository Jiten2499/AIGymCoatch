import { describe, expect, it } from '@jest/globals';
import { destinationForPush } from './pushNavigation';

describe('destinationForPush', () => {
  it('opens the conversation for a chat alert', () => {
    expect(destinationForPush({ type: 'chat.message', conversationId: 'thread-1' })).toEqual({
      name: 'ChatThread',
      params: { conversationId: 'thread-1', title: 'Chat', role: 'Member' },
    });
  });

  it('opens the workout plan when a trainer publishes an update', () => {
    expect(destinationForPush({ type: 'workout.plan.updated', referenceId: 'plan-1' })).toEqual({
      name: 'WorkoutPlan',
      params: { programId: 'plan-1' },
    });
  });

  it('opens today\'s plan for a reminder and the summary after completion', () => {
    expect(destinationForPush({ type: 'workout.reminder', referenceId: 'plan-2' })?.name).toBe('WorkoutPlan');
    expect(destinationForPush({ type: 'workout.completed', referenceId: 'session-1' })).toEqual({
      name: 'WorkoutSummary',
      params: { sessionId: 'session-1' },
    });
  });

  it('keeps account and trainer alerts on existing screens', () => {
    expect(destinationForPush({ type: 'trainer.assigned' })?.name).toBe('Profile');
    expect(destinationForPush({ type: 'account.updated' })?.name).toBe('Notifications');
    expect(destinationForPush({ type: 'progress.milestone' })?.name).toBe('Challenges');
  });

  it('does not invent a route when the payload has no destination', () => {
    expect(destinationForPush({})).toBeNull();
  });
});
