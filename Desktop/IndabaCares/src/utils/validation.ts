import { z } from 'zod';

export const sendRecognitionSchema = z.object({
  recipientIds: z
    .array(z.string().uuid())
    .min(1, 'Select at least one recipient')
    .max(10, 'Maximum 10 recipients'),
  thumbsUpTypeId: z.string().uuid('Select a recognition type'),
  message: z
    .string()
    .min(10, 'Message must be at least 10 characters')
    .max(2000, 'Message must be 2000 characters or fewer'),
  visibility: z.enum(['public', 'team_only', 'private']).default('public'),
  imageUrl: z.string().url().optional(),
  hashtags: z.array(z.string()).max(5).default([]),
});

export const submitMoodSchema = z.object({
  mood: z.enum(['awful', 'bad', 'okay', 'good', 'amazing']),
  note: z.string().max(500).optional(),
});

export const employeeCodeSchema = z.object({
  code: z
    .string()
    .min(4, 'Code must be at least 4 characters')
    .max(16, 'Code must be 16 characters or fewer')
    .regex(/^[A-Z0-9]+$/, 'Code must be uppercase letters and numbers only')
    .transform((v) => v.trim().toUpperCase()),
});

export type EmployeeCodeInput = z.infer<typeof employeeCodeSchema>;

export const editProfileSchema = z.object({
  displayName: z.string().min(2).max(50).optional(),
  jobTitle: z.string().max(100).optional(),
});

export type SendRecognitionInput = z.infer<typeof sendRecognitionSchema>;
export type SubmitMoodInput = z.infer<typeof submitMoodSchema>;
export type EditProfileInput = z.infer<typeof editProfileSchema>;
