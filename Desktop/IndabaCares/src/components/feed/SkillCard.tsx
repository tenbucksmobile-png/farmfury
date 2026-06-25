import React, { memo, useRef, useState } from 'react';
import {
  View, Text, TouchableOpacity, StyleSheet, Modal, Dimensions,
} from 'react-native';
import { Image } from 'expo-image';
import { LinearGradient } from 'expo-linear-gradient';
import { Avatar } from '@/components/ui/Avatar';
import { useEmployee } from '@/providers/EmployeeContext';
import { useSubmitResponse, RESPONSE_OPTIONS } from '@/hooks/use-recognition-response';
import {
  useRecognitionReactions,
  useSubmitReaction,
  type ReactionType,
} from '@/hooks/use-recognition-reactions';
import { useReactionBalance } from '@/hooks/use-reaction-balance';
import { ReactionExhaustedModal } from '@/components/reactions/ReactionExhaustedModal';
import type { RecognitionFeedItem } from '@/api/queries';
import { usedLogo } from '@/lib/localImages';

const LOGO = { uri: usedLogo };

const SKILL_BADGES = [
  { value: 'Leadership',       emoji: '👑', color: '#fbbf24' },
  { value: 'Teamwork',         emoji: '🤝', color: '#60a5fa' },
  { value: 'Communication',    emoji: '💬', color: '#34d399' },
  { value: 'Problem Solving',  emoji: '🧩', color: '#c084fc' },
  { value: 'Customer Service', emoji: '🌟', color: '#f472b6' },
  { value: 'Creativity',       emoji: '💡', color: '#fb923c' },
  { value: 'Reliability',      emoji: '⏰', color: '#22d3ee' },
  { value: 'Positivity',       emoji: '😊', color: '#a3e635' },
];

export const SKILL_BADGE_VALUES = new Set(SKILL_BADGES.map((b) => b.value));

const REACTIONS: { type: ReactionType; emoji: string }[] = [
  { type: 'heart',     emoji: '❤️' },
  { type: 'smile',     emoji: '😊' },
  { type: 'thumbs_up', emoji: '👍' },
];

function getSkillConfig(badge: string) {
  return (
    SKILL_BADGES.find((b) => b.value === badge) ?? {
      value: badge, emoji: '⭐', color: '#94a3b8',
    }
  );
}

function formatRelativeTime(iso: string): string {
  const diff = Date.now() - new Date(iso).getTime();
  const m = Math.floor(diff / 60_000);
  if (m < 1)  return 'just now';
  if (m < 60) return `${m}m ago`;
  const h = Math.floor(m / 60);
  if (h < 24) return `${h}h ago`;
  return `${Math.floor(h / 24)}d ago`;
}

interface SkillCardProps {
  recognition: RecognitionFeedItem;
}

export const SkillCard = memo(function SkillCard({ recognition }: SkillCardProps) {
  const { employee } = useEmployee();

  // Response
  const [showResponseMenu, setShowResponseMenu] = useState(false);
  const [localResponse, setLocalResponse]       = useState<string | null>(
    recognition.recipient_response ?? null,
  );
  const submitResponse = useSubmitResponse(recognition.id);

  // Emoji picker overlay
  const [showEmojiPicker, setShowEmojiPicker] = useState(false);
  const [pickerY, setPickerY]                 = useState(0);
  const [pickerRight, setPickerRight]         = useState(24);
  const [exhaustedType, setExhaustedType]     = useState<ReactionType | null>(null);
  const reactBtnRef                           = useRef<TouchableOpacity>(null);

  const { data: reactions = [] } = useRecognitionReactions(recognition.id);
  const { data: balance }        = useReactionBalance();
  const submitReaction           = useSubmitReaction(recognition.id);

  const isRecipient  = employee?.employee_id === recognition.receiver.id;
  const skillConfig  = getSkillConfig(recognition.badge);
  const senderDept   = recognition.sender.department   ?? recognition.sender.position   ?? null;
  const receiverDept = recognition.receiver.department ?? recognition.receiver.position ?? null;

  const myReaction = reactions.find((r) => r.employee_id === employee?.employee_id) ?? null;
  const counts = reactions.reduce<Record<ReactionType, number>>(
    (acc, r) => { acc[r.reaction_type] = (acc[r.reaction_type] ?? 0) + 1; return acc; },
    { heart: 0, smile: 0, thumbs_up: 0 },
  );
  const hasReactions = Object.values(counts).some((c) => c > 0);

  const handleOpenPicker = () => {
    reactBtnRef.current?.measure((_fx, _fy, _w, _h, _px, py) => {
      setPickerY(py);
      setPickerRight(Dimensions.get('window').width - (_px + _w));
      setShowEmojiPicker(true);
    });
  };

  const handleResponse = (text: string) => {
    setLocalResponse(text);
    setShowResponseMenu(false);
    submitResponse.mutate(text);
  };

  const handleReact = (type: ReactionType) => {
    if (submitReaction.isPending) return;
    setShowEmojiPicker(false);
    const existingId = myReaction?.reaction_type === type ? myReaction.id : null;
    submitReaction.mutate({ reactionType: type, existingId }, {
      onError: () => { if (!existingId) setExhaustedType(type); },
    });
  };

  return (
    <>
      <TouchableOpacity activeOpacity={0.97}>
        <LinearGradient
          colors={['#2d3748', '#374151', '#2d3748']}
          start={{ x: 0, y: 0 }}
          end={{ x: 1, y: 1 }}
          style={s.card}
        >
          {/* ── Logo watermark ────────────────────────────────── */}
          <Image source={LOGO} style={s.logo} contentFit="contain" />

          {/* ── Receiver row: avatar · name · dept · time ─────── */}
          <View style={s.receiverBlock}>
            <Avatar name={recognition.receiver.full_name} uri={recognition.receiver.photo_url ?? undefined} size="lg" />
            <View style={s.receiverInfo}>
              <View style={s.nameRow}>
                <Text style={s.receiverName} numberOfLines={1}>
                  {recognition.receiver.full_name}
                </Text>
                {receiverDept ? (
                  <Text style={s.receiverDept} numberOfLines={1}> · {receiverDept}</Text>
                ) : null}
              </View>
            </View>
            <Text style={s.timeAgo}>{formatRelativeTime(recognition.created_at)}</Text>
          </View>

          {/* ── Badge pill ────────────────────────────────────── */}
          <View style={[s.badgePill, { borderColor: skillConfig.color + '50' }]}>
            <Text style={s.badgeEmoji}>{skillConfig.emoji}</Text>
            <Text style={[s.badgeText, { color: skillConfig.color }]} numberOfLines={1}>{recognition.badge}</Text>
          </View>

          {/* ── Message ───────────────────────────────────────── */}
          <Text style={s.message} numberOfLines={4}>{recognition.message}</Text>

          {/* ── Given by ──────────────────────────────────────── */}
          <View style={s.givenByRow}>
            <Text style={s.givenByLabel}>Given by </Text>
            <Text style={s.givenByName}>{recognition.sender.full_name}</Text>
            {senderDept ? (
              <Text style={s.givenByDept} numberOfLines={1}> · {senderDept}</Text>
            ) : null}
          </View>

          {/* ── Response section ──────────────────────────────── */}
          {localResponse ? (
            <View style={s.responseDisplay}>
              <Text style={s.responseQuote}>"{localResponse}"</Text>
            </View>
          ) : (
            <>
              <TouchableOpacity
                style={[s.respondBtn, !isRecipient && s.respondBtnDisabled]}
                onPress={() => isRecipient && setShowResponseMenu((v) => !v)}
                activeOpacity={0.7}
              >
                <Text style={s.respondBtnText}>
                  {showResponseMenu ? 'Cancel' : 'Respond'}
                </Text>
              </TouchableOpacity>

              {showResponseMenu && (
                <View style={s.dropdown}>
                  {RESPONSE_OPTIONS.map((opt, i) => (
                    <TouchableOpacity
                      key={opt}
                      style={[s.dropdownOption, i === RESPONSE_OPTIONS.length - 1 && s.dropdownOptionLast]}
                      onPress={() => handleResponse(opt)}
                      activeOpacity={0.7}
                    >
                      <Text style={s.dropdownText}>{opt}</Text>
                    </TouchableOpacity>
                  ))}
                </View>
              )}
            </>
          )}

          {/* ── Reactions row ─────────────────────────────────── */}
          <View style={s.reactionsRow}>
            {hasReactions && (
              <View style={s.countsRow}>
                {REACTIONS.map(({ type, emoji }) => {
                  const count    = counts[type];
                  const isActive = myReaction?.reaction_type === type;
                  if (!count) return null;
                  return (
                    <View key={type} style={[s.countPill, isActive && s.countPillActive]}>
                      <Text style={s.countEmoji}>{emoji}</Text>
                      <Text style={[s.countText, isActive && s.countTextActive]}>{count}</Text>
                    </View>
                  );
                })}
              </View>
            )}
            <TouchableOpacity
              ref={reactBtnRef}
              style={s.reactBtn}
              onPress={handleOpenPicker}
              activeOpacity={0.7}
            >
              <Text style={s.reactBtnText}>{showEmojiPicker ? '✕' : '😊'}</Text>
            </TouchableOpacity>
          </View>

        </LinearGradient>
      </TouchableOpacity>

      {/* ── Emoji picker — floats over card as a modal ────────── */}
      <Modal
        transparent
        visible={showEmojiPicker}
        animationType="fade"
        statusBarTranslucent
        onRequestClose={() => setShowEmojiPicker(false)}
      >
        {/* Backdrop — tap to close */}
        <TouchableOpacity
          style={StyleSheet.absoluteFillObject}
          activeOpacity={1}
          onPress={() => setShowEmojiPicker(false)}
        />
        {/* Emoji tray — single container, floated above button */}
        <View style={[s.emojiTray, { top: pickerY - 68, right: pickerRight }]}>
          {REACTIONS.map(({ type, emoji }) => {
            const isActive = myReaction?.reaction_type === type;
            return (
              <TouchableOpacity
                key={type}
                style={[s.emojiBtn, isActive && s.emojiBtnActive]}
                onPress={() => handleReact(type)}
                activeOpacity={0.7}
              >
                <Text style={s.emojiText}>{emoji}</Text>
                {isActive && <View style={s.activeDot} />}
              </TouchableOpacity>
            );
          })}
        </View>
      </Modal>

      {exhaustedType && balance && (
        <ReactionExhaustedModal
          visible
          onClose={() => setExhaustedType(null)}
          exhaustedType={exhaustedType}
          balance={balance}
        />
      )}
    </>
  );
});

// ─── Styles ───────────────────────────────────────────────────────────────────

const s = StyleSheet.create({
  card: {
    borderRadius: 20,
    marginBottom: 12,
    paddingHorizontal: 14,
    paddingTop: 12,
    paddingBottom: 14,
    overflow: 'hidden',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.25,
    shadowRadius: 10,
    elevation: 6,
  },

  // Logo
  logo: {
    position: 'absolute',
    bottom: 4,
    right: 6,
    width: 70,
    height: 70,
    opacity: 0.5,
    tintColor: '#ffffff',
  },

  // Receiver row
  receiverBlock: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
    marginBottom: 10,
  },
  receiverInfo: {
    flex: 1,
  },
  nameRow: {
    flexDirection: 'row',
    alignItems: 'baseline',
    flexWrap: 'wrap',
  },
  receiverName: {
    fontSize: 16,
    fontWeight: '800',
    color: '#ffffff',
  },
  receiverDept: {
    fontSize: 13,
    color: 'rgba(255,255,255,0.55)',
  },
  timeAgo: {
    fontSize: 11,
    color: 'rgba(255,255,255,0.45)',
    alignSelf: 'flex-start',
    paddingTop: 2,
  },

  // Badge pill
  badgePill: {
    flexDirection: 'row',
    alignItems: 'center',
    alignSelf: 'flex-start',
    borderRadius: 20,
    paddingHorizontal: 12,
    paddingVertical: 5,
    backgroundColor: 'rgba(255,255,255,0.08)',
    borderWidth: 1,
    marginBottom: 10,
  },
  badgeEmoji: { fontSize: 14 },
  badgeText: {
    fontSize: 13,
    fontWeight: '700',
    marginLeft: 6,
  },

  // Message
  message: {
    fontSize: 14,
    lineHeight: 21,
    color: 'rgba(255,255,255,0.88)',
    marginBottom: 10,
  },

  // Given by
  givenByRow: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: 'rgba(255,255,255,0.08)',
    borderRadius: 10,
    paddingHorizontal: 10,
    paddingVertical: 6,
    marginBottom: 10,
    flexWrap: 'wrap',
  },
  givenByLabel: {
    fontSize: 12,
    color: 'rgba(255,255,255,0.5)',
    fontWeight: '600',
  },
  givenByName: {
    fontSize: 12,
    color: '#ffffff',
    fontWeight: '700',
  },
  givenByDept: {
    fontSize: 12,
    color: 'rgba(255,255,255,0.5)',
    flex: 1,
  },

  // Response display
  responseDisplay: {
    backgroundColor: 'rgba(255,255,255,0.1)',
    borderRadius: 10,
    paddingHorizontal: 12,
    paddingVertical: 10,
    marginBottom: 12,
  },
  responseQuote: {
    fontSize: 13,
    color: 'rgba(255,255,255,0.8)',
    fontStyle: 'italic',
    fontWeight: '600',
  },

  // Respond button
  respondBtn: {
    alignSelf: 'flex-start',
    backgroundColor: 'rgba(255,255,255,0.12)',
    borderRadius: 20,
    borderWidth: 1,
    borderColor: 'rgba(255,255,255,0.3)',
    paddingHorizontal: 16,
    paddingVertical: 6,
    marginBottom: 8,
  },
  respondBtnDisabled: {
    opacity: 0.4,
  },
  respondBtnText: {
    fontSize: 13,
    fontWeight: '700',
    color: '#ffffff',
  },

  // Dropdown
  dropdown: {
    backgroundColor: 'rgba(255,255,255,0.95)',
    borderRadius: 14,
    marginBottom: 12,
    overflow: 'hidden',
  },
  dropdownOption: {
    paddingHorizontal: 16,
    paddingVertical: 10,
    borderBottomWidth: 1,
    borderBottomColor: '#e2e8f0',
  },
  dropdownOptionLast: {
    borderBottomWidth: 0,
  },
  dropdownText: {
    fontSize: 14,
    color: '#1e293b',
    fontWeight: '600',
  },

  // Reactions row
  reactionsRow: {
    flexDirection: 'row',
    alignItems: 'center',
    marginTop: 4,
  },
  countsRow: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 6,
    flex: 1,
  },
  countPill: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: 'rgba(255,255,255,0.12)',
    borderRadius: 20,
    paddingHorizontal: 8,
    paddingVertical: 4,
  },
  countPillActive: {
    backgroundColor: 'rgba(255,255,255,0.25)',
    borderWidth: 1,
    borderColor: 'rgba(255,255,255,0.45)',
  },
  countEmoji: { fontSize: 13 },
  countText: {
    fontSize: 12,
    fontWeight: '600',
    color: 'rgba(255,255,255,0.75)',
    marginLeft: 3,
  },
  countTextActive: { color: '#ffffff' },

  // React button
  reactBtn: {
    width: 36,
    height: 36,
    borderRadius: 18,
    backgroundColor: 'rgba(255,255,255,0.12)',
    borderWidth: 1,
    borderColor: 'rgba(255,255,255,0.25)',
    alignItems: 'center',
    justifyContent: 'center',
    marginLeft: 8,
  },
  reactBtnText: { fontSize: 18 },

  // ── Emoji tray (modal overlay) ────────────────────────────────────────────
  emojiTray: {
    position: 'absolute',
    flexDirection: 'row',
    gap: 8,
    backgroundColor: '#1e2530',
    borderRadius: 40,
    paddingHorizontal: 14,
    paddingVertical: 10,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.35,
    shadowRadius: 12,
    elevation: 12,
    borderWidth: 1,
    borderColor: 'rgba(255,255,255,0.12)',
  },
  emojiBtn: {
    width: 44,
    height: 44,
    borderRadius: 22,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: 'rgba(255,255,255,0.06)',
  },
  emojiBtnActive: {
    backgroundColor: 'rgba(255,255,255,0.2)',
    borderWidth: 1,
    borderColor: 'rgba(255,255,255,0.4)',
  },
  emojiText: { fontSize: 26 },
  activeDot: {
    position: 'absolute',
    bottom: 4,
    width: 5,
    height: 5,
    borderRadius: 3,
    backgroundColor: '#ffffff',
  },
});
