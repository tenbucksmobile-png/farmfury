/**
 * Supabase Database Types
 *
 * In production, generate with: npx supabase gen types typescript --linked > src/types/database.ts
 * These manual types match the schema from migrations 001-009.
 */

export type AppRole = 'employee' | 'manager' | 'admin' | 'super_admin';
export type Visibility = 'public' | 'team_only' | 'private';
export type MoodValue = 'awful' | 'bad' | 'okay' | 'good' | 'amazing';
export type RedemptionStatus = 'pending' | 'approved' | 'preparing' | 'shipped' | 'fulfilled' | 'rejected' | 'cancelled';
export type NotificationType =
  | 'recognition_received'
  | 'recognition_boosted'
  | 'reaction'
  | 'comment'
  | 'reward_approved'
  | 'reward_in_preparation'
  | 'reward_shipped'
  | 'reward_fulfilled'
  | 'reward_rejected'
  | 'budget_reset'
  | 'badge_earned'
  | 'manager_alert'
  | 'system';

// ─── Row types ──────────────────────────────────────────────────────────────

type CompanyRow = {
  id: string;
  name: string;
  slug: string;
  logo_url: string | null;
  primary_color: string;
  settings: Record<string, unknown>;
  created_at: string;
  updated_at: string;
}

type DepartmentRow = {
  id: string;
  company_id: string;
  name: string;
  created_at: string;
}

type ProfileRow = {
  id: string;
  company_id: string;
  email: string;
  full_name: string;
  display_name: string | null;
  avatar_url: string | null;
  role: AppRole;
  department_id: string | null;
  manager_id: string | null;
  job_title: string | null;
  points_balance: number;
  stars_balance: number;
  giving_balance: number;
  login_streak: number;
  last_mood_date: string | null;
  is_active: boolean;
  created_at: string;
  updated_at: string;
}

type CompanyValueRow = {
  id: string;
  company_id: string;
  name: string;
  description: string | null;
  icon: string;
  sort_order: number;
  is_active: boolean;
  created_at: string;
}

type ThumbsUpTypeRow = {
  id: string;
  company_id: string;
  name: string;
  icon: string;
  color: string;
  stars_awarded: number;
  description: string | null;
  sort_order: number;
  is_active: boolean;
  created_at: string;
}

type RecognitionRow = {
  id: string;
  company_id: string;
  sender_id: string;
  thumbs_up_type_id: string;
  message: string;
  visibility: Visibility;
  stars_per_recipient: number;
  image_url: string | null;
  hashtags: string[];
  is_boosted: boolean;
  boosted_by: string | null;
  boosted_at: string | null;
  created_at: string;
}

type RecognitionRecipientRow = {
  id: string;
  recognition_id: string;
  recipient_id: string;
}

type ReactionRow = {
  id: string;
  company_id: string;
  recognition_id: string;
  user_id: string;
  emoji: string;
  created_at: string;
}

type CommentRow = {
  id: string;
  company_id: string;
  recognition_id: string;
  user_id: string;
  body: string;
  created_at: string;
  updated_at: string;
}

type NotificationRow = {
  id: string;
  employee_id: string;
  hotel: string;
  type: string;
  title: string;
  message: string | null;
  read: boolean;
  reference_type: string | null;
  reference_id: string | null;
  created_at: string;
}

type RewardRow = {
  id: string;
  hotel: string;
  title: string;
  description: string | null;
  image_url: string | null;
  points_required: number;
  stock: number | null;
  category: string | null;
  terms: string | null;
  is_active: boolean;
  created_at: string;
}

type RewardCategoryRow = {
  id: string;
  company_id: string;
  name: string;
  sort_order: number;
  created_at: string;
}

type RedemptionRow = {
  id: string;
  employee_id: string;
  reward_id: string;
  hotel: string;
  points_used: number;
  status: RedemptionStatus;
  approved_at: string | null;
  rejected_at: string | null;
  fulfilled_at: string | null;
  rejection_reason: string | null;
  created_at: string;
  updated_at: string;
}

type MoodEntryRow = {
  id: string;
  company_id: string;
  user_id: string;
  mood: MoodValue;
  note: string | null;
  entry_date: string;
  created_at: string;
}

type SkillCategoryRow = {
  id: string;
  company_id: string;
  name: string;
  sort_order: number;
  created_at: string;
}

type SkillIndicatorRow = {
  id: string;
  category_id: string;
  company_id: string;
  name: string;
  description: string | null;
  sort_order: number;
  created_at: string;
}

type SkillRatingRow = {
  id: string;
  company_id: string;
  rater_id: string;
  recipient_id: string;
  indicator_id: string;
  score: number;
  created_at: string;
}

type BadgeRow = {
  id: string;
  company_id: string | null;
  hotel: string | null;
  slug: string;
  name: string;
  description: string | null;
  icon: string;
  criteria: Record<string, unknown>;
  created_at: string;
}

// ─── Hotel-based schema row types ────────────────────────────────────────────

type EmployeeRow = {
  id: string;
  employee_code: string;
  full_name: string;
  hotel: string;
  department: string | null;
  position: string | null;
  job_title: string | null;
  status: string;
  photo_url: string | null;
  avatar_url: string | null;
  points_balance: number;
  deleted_at: string | null;
  created_at: string;
}

type MessageRow = {
  id: string;
  sender_id: string;
  hotel: string;
  body: string;
  created_at: string;
}

type UserBadgeRow = {
  id: string;
  user_id: string;
  badge_id: string;
  earned_at: string;
}

type LeaderboardCacheRow = {
  id: string;
  company_id: string;
  user_id: string;
  period_type: string;
  period_key: string;
  total_points: number;
  rank: number;
  rank_change: number;
  refreshed_at: string;
}

type PointTransactionRow = {
  id: string;
  company_id: string;
  user_id: string;
  type: string;
  amount: number;
  balance_after: number;
  reference_type: string | null;
  reference_id: string | null;
  description: string | null;
  idempotency_key: string;
  created_at: string;
}

type StarTransactionRow = {
  id: string;
  company_id: string;
  user_id: string;
  type: string;
  amount: number;
  balance_after: number;
  reference_type: string | null;
  reference_id: string | null;
  description: string | null;
  idempotency_key: string;
  created_at: string;
}

// ─── Additional hotel-based table types ──────────────────────────────────────

type ReactionAllocationRow = {
  id: string;
  employee_id: string;
  hotel: string;
  month: number;
  year: number;
  hearts_remaining: number;
  smiles_remaining: number;
  thumbs_remaining: number;
  updated_at: string;
}

type RecognitionLikeRow = {
  id: string;
  recognition_id: string;
  employee_id: string;
  hotel: string;
  created_at: string;
}

type RecognitionCommentRow = {
  id: string;
  recognition_id: string;
  employee_id: string;
  hotel: string;
  body: string;
  created_at: string;
}

type PointsLedgerRow = {
  id: string;
  employee_id: string;
  hotel: string;
  points: number;
  source: string;
  reference_id: string | null;
  created_at: string;
}

type RecognitionReactionRow = {
  id: string;
  recognition_id: string;
  employee_id: string;
  hotel: string;
  reaction_type: string;
  created_at: string;
}

type InitiativeRow = {
  id: string;
  hotel: string;
  tab: string;
  mascot_url: string | null;
  image_urls: string[];
  video_url: string | null;
  sort_order: number;
  created_at: string;
}

type MonthlyLegendRow = {
  id: string;
  hotel: string;
  employee_id: string;
  full_name: string;
  job_title: string | null;
  avatar_url: string | null;
  month: number;
  year: number;
  total_points: number;
  points_awarded: number;
  created_at: string;
}

type ChannelPostRow = {
  id:            string;
  hotel:         string;
  post_type:     'photo' | 'video' | 'text';
  media_url:     string | null;
  media_path:    string | null;
  thumbnail_url: string | null;
  caption:       string | null;
  created_by:    string;
  created_at:    string;
  is_published:  boolean;
}

// ─── Database interface ─────────────────────────────────────────────────────

export interface Database {
  public: {
    Tables: {
      companies: {
        Row: CompanyRow;
        Insert: Omit<CompanyRow, 'id' | 'created_at' | 'updated_at'>;
        Update: Partial<Omit<CompanyRow, 'id' | 'created_at' | 'updated_at'>>;
        Relationships: [];
      };
      departments: {
        Row: DepartmentRow;
        Insert: Omit<DepartmentRow, 'id' | 'created_at'>;
        Update: Partial<Omit<DepartmentRow, 'id' | 'created_at'>>;
        Relationships: [];
      };
      profiles: {
        Row: ProfileRow;
        Insert: Omit<ProfileRow, 'points_balance' | 'stars_balance' | 'giving_balance' | 'login_streak' | 'created_at' | 'updated_at'>;
        Update: Partial<Omit<ProfileRow, 'id' | 'created_at' | 'updated_at'>>;
        Relationships: [];
      };
      company_values: {
        Row: CompanyValueRow;
        Insert: Omit<CompanyValueRow, 'id' | 'created_at'>;
        Update: Partial<Omit<CompanyValueRow, 'id' | 'created_at'>>;
        Relationships: [];
      };
      thumbs_up_types: {
        Row: ThumbsUpTypeRow;
        Insert: Omit<ThumbsUpTypeRow, 'id' | 'created_at'>;
        Update: Partial<Omit<ThumbsUpTypeRow, 'id' | 'created_at'>>;
        Relationships: [];
      };
      recognitions: {
        Row: RecognitionRow;
        Insert: Omit<RecognitionRow, 'id' | 'created_at'>;
        Update: Partial<Omit<RecognitionRow, 'id' | 'created_at'>>;
        Relationships: [];
      };
      recognition_recipients: {
        Row: RecognitionRecipientRow;
        Insert: Omit<RecognitionRecipientRow, 'id'>;
        Update: Partial<Omit<RecognitionRecipientRow, 'id'>>;
        Relationships: [];
      };
      reactions: {
        Row: ReactionRow;
        Insert: Omit<ReactionRow, 'id' | 'created_at' | 'user_id'>;
        Update: Partial<Omit<ReactionRow, 'id' | 'created_at'>>;
        Relationships: [];
      };
      comments: {
        Row: CommentRow;
        Insert: Omit<CommentRow, 'id' | 'created_at' | 'updated_at' | 'user_id'>;
        Update: Partial<Omit<CommentRow, 'id' | 'created_at' | 'updated_at'>>;
        Relationships: [];
      };
      notifications: {
        Row: NotificationRow;
        Insert: Omit<NotificationRow, 'id' | 'created_at' | 'is_read'>;
        Update: Partial<Omit<NotificationRow, 'id' | 'created_at'>>;
        Relationships: [];
      };
      rewards: {
        Row: RewardRow;
        Insert: Omit<RewardRow, 'id' | 'created_at' | 'updated_at'>;
        Update: Partial<Omit<RewardRow, 'id' | 'created_at' | 'updated_at'>>;
        Relationships: [];
      };
      reward_categories: {
        Row: RewardCategoryRow;
        Insert: Omit<RewardCategoryRow, 'id' | 'created_at'>;
        Update: Partial<Omit<RewardCategoryRow, 'id' | 'created_at'>>;
        Relationships: [];
      };
      redemptions: {
        Row: RedemptionRow;
        Insert: Omit<RedemptionRow, 'id' | 'created_at' | 'updated_at'>;
        Update: Partial<Omit<RedemptionRow, 'id' | 'created_at' | 'updated_at'>>;
        Relationships: [];
      };
      mood_entries: {
        Row: MoodEntryRow;
        Insert: Omit<MoodEntryRow, 'id' | 'created_at'>;
        Update: Partial<Omit<MoodEntryRow, 'id' | 'created_at'>>;
        Relationships: [];
      };
      skill_categories: {
        Row: SkillCategoryRow;
        Insert: Omit<SkillCategoryRow, 'id' | 'created_at'>;
        Update: Partial<Omit<SkillCategoryRow, 'id' | 'created_at'>>;
        Relationships: [];
      };
      skill_indicators: {
        Row: SkillIndicatorRow;
        Insert: Omit<SkillIndicatorRow, 'id' | 'created_at'>;
        Update: Partial<Omit<SkillIndicatorRow, 'id' | 'created_at'>>;
        Relationships: [];
      };
      skill_ratings: {
        Row: SkillRatingRow;
        Insert: Omit<SkillRatingRow, 'id' | 'created_at'>;
        Update: Partial<Omit<SkillRatingRow, 'id' | 'created_at'>>;
        Relationships: [];
      };
      badges: {
        Row: BadgeRow;
        Insert: Omit<BadgeRow, 'id' | 'created_at'>;
        Update: Partial<Omit<BadgeRow, 'id' | 'created_at'>>;
        Relationships: [];
      };
      user_badges: {
        Row: UserBadgeRow;
        Insert: Omit<UserBadgeRow, 'id' | 'earned_at'>;
        Update: Partial<Omit<UserBadgeRow, 'id' | 'earned_at'>>;
        Relationships: [];
      };
      leaderboard_cache: {
        Row: LeaderboardCacheRow;
        Insert: Omit<LeaderboardCacheRow, 'id'>;
        Update: Partial<Omit<LeaderboardCacheRow, 'id'>>;
        Relationships: [];
      };
      point_transactions: {
        Row: PointTransactionRow;
        Insert: Omit<PointTransactionRow, 'id' | 'created_at'>;
        Update: Record<string, never>;
        Relationships: [];
      };
      star_transactions: {
        Row: StarTransactionRow;
        Insert: Omit<StarTransactionRow, 'id' | 'created_at'>;
        Update: Record<string, never>;
        Relationships: [];
      };
      employees: {
        Row: EmployeeRow;
        Insert: Omit<EmployeeRow, 'id' | 'created_at'>;
        Update: Partial<Omit<EmployeeRow, 'id' | 'created_at'>>;
        Relationships: [];
      };
      messages: {
        Row: MessageRow;
        Insert: Omit<MessageRow, 'id' | 'created_at'>;
        Update: Partial<Omit<MessageRow, 'id' | 'created_at'>>;
        Relationships: [];
      };
      employee_reaction_allocations: {
        Row: ReactionAllocationRow;
        Insert: Omit<ReactionAllocationRow, 'id'>;
        Update: Partial<Omit<ReactionAllocationRow, 'id'>>;
        Relationships: [];
      };
      recognition_likes: {
        Row: RecognitionLikeRow;
        Insert: Omit<RecognitionLikeRow, 'id' | 'created_at'>;
        Update: Partial<Omit<RecognitionLikeRow, 'id' | 'created_at'>>;
        Relationships: [];
      };
      recognition_comments: {
        Row: RecognitionCommentRow;
        Insert: Omit<RecognitionCommentRow, 'id' | 'created_at'>;
        Update: Partial<Omit<RecognitionCommentRow, 'id' | 'created_at'>>;
        Relationships: [];
      };
      points_ledger: {
        Row: PointsLedgerRow;
        Insert: Omit<PointsLedgerRow, 'id' | 'created_at'>;
        Update: Record<string, never>;
        Relationships: [];
      };
      recognition_reactions: {
        Row: RecognitionReactionRow;
        Insert: Omit<RecognitionReactionRow, 'id' | 'created_at'>;
        Update: Partial<Omit<RecognitionReactionRow, 'id' | 'created_at'>>;
        Relationships: [];
      };
      initiatives: {
        Row: InitiativeRow;
        Insert: Omit<InitiativeRow, 'id' | 'created_at'>;
        Update: Partial<Omit<InitiativeRow, 'id' | 'created_at'>>;
        Relationships: [];
      };
      monthly_legends: {
        Row: MonthlyLegendRow;
        Insert: Omit<MonthlyLegendRow, 'id' | 'created_at'>;
        Update: Partial<Omit<MonthlyLegendRow, 'id' | 'created_at'>>;
        Relationships: [];
      };
      channel_posts: {
        Row: ChannelPostRow;
        Insert: Omit<ChannelPostRow, 'id' | 'created_at'>;
        Update: Partial<Omit<ChannelPostRow, 'id' | 'created_at'>>;
        Relationships: [];
      };
    };
    Views: {
      [_ in never]: never;
    };
    Functions: {
      // ── Legacy company-based RPCs (retained for compatibility) ─────────────
      process_recognition: {
        Args: {
          p_sender_id: string;
          p_company_id: string;
          p_recipient_ids: string[];
          p_thumbs_up_type_id: string;
          p_message: string;
          p_visibility: string;
          p_image_url: string | null;
          p_hashtags: string[];
        };
        Returns: string;
      };
      process_redemption: {
        Args: { p_user_id: string; p_company_id: string; p_reward_id: string };
        Returns: string;
      };
      track_login: {
        Args: { target_user_id: string };
        Returns: unknown;
      };
      resolve_budget: {
        Args: { target_user_id: string };
        Returns: unknown;
      };
      // ── Hotel-based auth RPCs ──────────────────────────────────────────────
      first_time_authenticate: {
        Args: {
          p_employee_code: string;
          p_hotel: string;
          p_full_name: string;
          p_new_password: string;
        };
        Returns: {
          ok: boolean;
          id: string;
          full_name: string;
          employee_code: string;
          hotel: string;
          department: string | null;
          token: string;
          error?: string;
        };
      };
      authenticate_employee: {
        Args: {
          p_employee_code: string;
          p_hotel: string;
          p_password: string;
        };
        Returns: {
          ok: boolean;
          id: string;
          full_name: string;
          employee_code: string;
          hotel: string;
          department: string | null;
          token: string;
          error?: string;
        };
      };
      validate_session: {
        Args: { p_session_token: string };
        Returns: { ok: boolean; error?: string };
      };
      revoke_employee_session: {
        Args: { p_token: string };
        Returns: void;
      };
      // ── Push notifications ─────────────────────────────────────────────────
      upsert_push_token: {
        Args: {
          p_employee_id: string;
          p_hotel: string;
          p_token: string;
          p_platform: string;
        };
        Returns: void;
      };
      // ── Hotel settings / feature flags ────────────────────────────────────
      get_hotel_settings: {
        Args: { p_hotel: string };
        Returns: Record<string, boolean>;
      };
      // ── Mood ──────────────────────────────────────────────────────────────
      submit_mood: {
        Args: {
          p_employee_id: string;
          p_hotel: string;
          p_mood: MoodValue;
          p_note: string | null;
        };
        Returns: string;
      };
      // ── Chat ──────────────────────────────────────────────────────────────
      get_chat_messages: {
        Args: {
          p_hotel: string;
          p_limit: number;
          p_before_timestamp: string | null;
        };
        Returns: {
          id: string;
          body: string;
          hotel: string;
          created_at: string;
          sender: {
            id: string;
            full_name: string;
            employee_code: string;
            position: string | null;
          } | null;
        }[];
      };
      // ── Leaderboard ───────────────────────────────────────────────────────
      get_leaderboard: {
        Args: {
          p_hotel: string;
          p_start: string | null;
          p_end: string | null;
          p_limit: number;
        };
        Returns: unknown[];
      };
      // ── Notifications ─────────────────────────────────────────────────────
      mark_notification_read: {
        Args: { p_id: string };
        Returns: void;
      };
      mark_all_notifications_read: {
        Args: { p_employee_id: string };
        Returns: void;
      };
      // ── Reactions ─────────────────────────────────────────────────────────
      submit_recognition_reaction: {
        Args: {
          p_recognition_id: string;
          p_employee_id: string;
          p_reaction_type: string;
        };
        Returns: { ok: boolean; reaction_id: string; error?: string };
      };
      submit_recognition_response: {
        Args: {
          p_recognition_id: string;
          p_employee_id: string | undefined;
          p_response: string;
        };
        Returns: { ok: boolean; error?: string };
      };
      // ── Rewards / redemptions ─────────────────────────────────────────────
      redeem_reward: {
        Args: { p_employee_id: string; p_reward_id: string };
        Returns: { ok: boolean; redemption_id?: string; error?: string };
      };
      approve_redemption: {
        Args: { p_redemption_id: string };
        Returns: { ok: boolean; error?: string };
      };
      reject_redemption: {
        Args: { p_redemption_id: string; p_reason: string | null };
        Returns: { ok: boolean; error?: string };
      };
      fulfill_redemption: {
        Args: { p_redemption_id: string };
        Returns: { ok: boolean; error?: string };
      };
      cancel_redemption: {
        Args: { p_redemption_id: string };
        Returns: { ok: boolean; error?: string };
      };
      // ── Search ────────────────────────────────────────────────────────────
      search_recognitions: {
        Args: { p_hotel: string; p_search: string; p_limit: number };
        Returns: unknown[];
      };
      // ── Reaction analytics ────────────────────────────────────────────────
      get_top_reactors: {
        Args: {
          p_hotel: string;
          p_limit: number;
          p_start?: string | null;
          p_end?: string | null;
        };
        Returns: unknown[];
      };
      get_top_recognised_employees: {
        Args: {
          p_hotel: string;
          p_limit: number;
          p_start?: string | null;
          p_end?: string | null;
        };
        Returns: unknown[];
      };
      get_most_reacted_recognitions: {
        Args: {
          p_hotel: string;
          p_limit: number;
          p_start?: string | null;
          p_end?: string | null;
        };
        Returns: unknown[];
      };
      get_reaction_hotel_summary: {
        Args: {
          p_hotel: string;
          p_start?: string | null;
          p_end?: string | null;
        };
        Returns: unknown[];
      };
      // ── Account deletion ──────────────────────────────────────────────────
      delete_employee_account: {
        Args: { p_employee_id: string; p_hotel: string };
        Returns: { ok: boolean; error?: string };
      };
      // ── Profile ───────────────────────────────────────────────────────────
      update_employee_avatar: {
        Args: { p_photo_url: string };
        Returns: void;
      };
      // ── Campaigns ─────────────────────────────────────────────────────────
      get_active_campaigns: {
        Args: { p_hotel?: string };
        Returns: unknown[];
      };
      get_campaigns_for_hotel: {
        Args: { p_hotel: string };
        Returns: {
          id:                   string;
          title:                string;
          description:          string | null;
          type:                 string;
          points_multiplier:    number;
          hotel:                string;
          start_date:           string;
          end_date:             string;
          days_remaining:       number;
          is_active:            boolean;
          sponsor_name:         string | null;
          banner_url:           string | null;
          banner_link_url:      string | null;
          voucher_description:  string | null;
        }[];
      };
    };
    Enums: {
      app_role: AppRole;
      mood_value: MoodValue;
      notification_type: NotificationType;
      recognition_visibility: Visibility;
      redemption_status: RedemptionStatus;
    };
    CompositeTypes: {
      [_ in never]: never;
    };
  };
}
