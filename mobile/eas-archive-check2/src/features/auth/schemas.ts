import { z } from 'zod';

const email = z.string().trim().email().max(256);

export const loginSchema = z.object({
  email,
  password: z.string().min(1),
});

export const registerSchema = z.object({
  email,
  password: z
    .string()
    .min(12)
    .regex(/[A-Z]/)
    .regex(/[a-z]/)
    .regex(/\d/)
    .regex(/[^A-Za-z0-9]/),
  firstName: z.string().trim().min(1).max(100),
  lastName: z.string().trim().min(1).max(100),
  phone: z.string().trim().max(32).optional(),
});

export const forgotSchema = z.object({
  email,
});

export type LoginValues = z.infer<typeof loginSchema>;
export type RegisterValues = z.infer<typeof registerSchema>;
export type ForgotValues = z.infer<typeof forgotSchema>;
