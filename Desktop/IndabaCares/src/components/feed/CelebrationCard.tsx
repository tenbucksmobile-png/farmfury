import React, { memo } from 'react';
import { View, Text, TouchableOpacity, StyleSheet } from 'react-native';
import { Image } from 'expo-image';
import { LinearGradient } from 'expo-linear-gradient';
import { Avatar } from '@/components/ui/Avatar';
import { useLikes, useToggleLike } from '@/hooks/use-likes';
import { useEmployee } from '@/providers/EmployeeContext';
import type { CelebrationFeedItem } from '@/hooks/use-celebrations';
import { usedLogo } from '@/lib/localImages';

const LOGO = { uri: usedLogo };

const MILESTONE_YEARS = [1, 5, 10, 15, 20, 25, 30, 35, 40];

function isMajorMilestone(years: number): boolean {
  return MILESTONE_YEARS.includes(years);
}

// ─── Component ────────────────────────────────────────────────────────────────

interface Props {
  celebration: CelebrationFeedItem;
}

export const CelebrationCard = memo(function CelebrationCard({ celebration }: Props) {
  const { employee: currentEmployee } = useEmployee();
  const { type, milestone, employee }  = celebration;
  const isBirthday = type === 'birthday';
  const isMilestone = !isBirthday && milestone != null && isMajorMilestone(milestone);

  const gradientColors: [string, string, string] = isBirthday
    ? ['#4a0000', '#b71c1c', '#dc2626']
    : ['#14532d', '#166534', '#16a34a'];

  const shadowColor = isBirthday ? '#7f0000' : '#14532d';

  const dept = employee.department ?? employee.position ?? null;

  const bodyText = isBirthday
    ? `Happy Birthday to ${employee.full_name}, we hope you have an awesome day full of fun, love and celebrations.`
    : isMilestone
      ? `Congratulations on achieving your milestone - we appreciate your long service and commitment.`
      : `${employee.full_name} is celebrating ${milestone} year${(milestone ?? 1) !== 1 ? 's' : ''} with the team today.`;

  // Heart — uses likes (no balance deduction)
  const { data: likes = [] }       = useLikes(celebration.id);
  const toggleLike                 = useToggleLike(celebration.id);
  const myLike                     = likes.find((l) => l.employee_id === currentEmployee?.employee_id);
  const liked                      = !!myLike;
  const likeCount                  = likes.length;
  const handleHeart                = () => toggleLike.mutate({ likeId: myLike?.id ?? null });

  // Thumbs up — separate key, no balance deduction
  const { data: thumbsLikes = [] } = useLikes(celebration.id + '_thumbs');
  const toggleThumb                = useToggleLike(celebration.id + '_thumbs');
  const myThumb                    = thumbsLikes.find((l) => l.employee_id === currentEmployee?.employee_id);
  const thumbed                    = !!myThumb;
  const thumbCount                 = thumbsLikes.length;
  const handleThumb                = () => toggleThumb.mutate({ likeId: myThumb?.id ?? null });

  return (
    <LinearGradient
      colors={gradientColors}
      start={{ x: 0, y: 0 }}
      end={{ x: 1, y: 1 }}
      style={[s.card, { shadowColor }]}
    >
      {/* ── Logo watermark ────────────────────────────────── */}
      <Image source={LOGO} style={s.logo} contentFit="contain" />

      {/* ── Header ────────────────────────────────────────── */}
      {isBirthday ? (
        <Text style={s.birthdayHeader}>
          🎉 Happy Birthday! 🎉
        </Text>
      ) : (
        <View style={s.milestoneHeaderRow}>
          <Text style={s.milestoneYear}>{milestone ?? ''}</Text>
          <View style={s.milestoneTitleWrap}>
            <Text style={s.milestoneTitle}>Service Milestone</Text>
          </View>
          <View style={s.milestoneYearSpacer} />
        </View>
      )}

      {/* ── Person row: avatar · name · dept ──────────────── */}
      <View style={s.personRow}>
        <Avatar name={employee.full_name} uri={employee.photo_url ?? undefined} size="lg" />
        <View style={s.personInfo}>
          <Text style={s.personName} numberOfLines={1} adjustsFontSizeToFit minimumFontScale={0.8}>
            {employee.full_name}
          </Text>
          {dept ? (
            <Text style={s.personDept} numberOfLines={1}>{dept}</Text>
          ) : null}
        </View>
      </View>

      {/* ── Body text ─────────────────────────────────────── */}
      <Text style={s.body}>{bodyText}</Text>

      {/* ── Reactions ─────────────────────────────────────── */}
      <View style={s.heartRow}>
        {/* Heart: birthday only */}
        {isBirthday && (
          <TouchableOpacity
            onPress={handleHeart}
            disabled={toggleLike.isPending}
            style={s.heartBtn}
            activeOpacity={0.7}
          >
            <Text style={s.heartEmoji}>{liked ? '❤️' : '🤍'}</Text>
            {likeCount > 0 && (
              <Text style={s.heartCount}>{likeCount}</Text>
            )}
          </TouchableOpacity>
        )}

        {/* Thumbs up: milestone only */}
        {!isBirthday && <TouchableOpacity
          onPress={handleThumb}
          disabled={toggleThumb.isPending}
          style={s.heartBtn}
          activeOpacity={0.7}
        >
          <Text style={[s.heartEmoji, !thumbed && { opacity: 0.5 }]}>👍</Text>
          {thumbCount > 0 && (
            <Text style={s.thumbCount}>{thumbCount}</Text>
          )}
        </TouchableOpacity>}
      </View>

    </LinearGradient>
  );
});

// ─── Styles ───────────────────────────────────────────────────────────────────

const s = StyleSheet.create({
  card: {
    borderRadius: 20,
    marginBottom: 12,
    paddingHorizontal: 14,
    paddingTop: 14,
    paddingBottom: 14,
    overflow: 'hidden',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.3,
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

  // Birthday header — centered, animated
  birthdayHeader: {
    fontFamily: 'DancingScript_700Bold',
    fontSize: 34,
    color: '#fff',
    textAlign: 'center',
    marginBottom: 12,
    textShadowColor: 'rgba(0,0,0,0.25)',
    textShadowOffset: { width: 0, height: 2 },
    textShadowRadius: 4,
  },

  // Milestone header row
  milestoneHeaderRow: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 12,
  },
  milestoneYear: {
    width: 68,
    fontSize: 50,
    fontWeight: '900',
    color: '#fff',
    lineHeight: 54,
    textShadowColor: 'rgba(0,0,0,0.3)',
    textShadowOffset: { width: 0, height: 2 },
    textShadowRadius: 6,
  },
  milestoneTitleWrap: {
    flex: 1,
    alignItems: 'center',
  },
  milestoneTitle: {
    fontFamily: 'DancingScript_700Bold',
    fontSize: 32,
    color: '#fff',
    textShadowColor: 'rgba(0,0,0,0.25)',
    textShadowOffset: { width: 0, height: 2 },
    textShadowRadius: 4,
  },
  milestoneYearSpacer: {
    width: 68,
  },

  // Person row
  personRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
    backgroundColor: 'rgba(255,255,255,0.1)',
    borderRadius: 12,
    paddingHorizontal: 10,
    paddingVertical: 7,
    marginBottom: 10,
  },
  personInfo: {
    flex: 1,
  },
  personName: {
    fontSize: 16,
    fontWeight: '800',
    color: '#fff',
  },
  personDept: {
    fontSize: 13,
    color: 'rgba(255,255,255,0.65)',
    marginTop: 1,
  },

  // Body
  body: {
    fontSize: 13,
    lineHeight: 20,
    color: '#fff',
    marginBottom: 10,
    textAlign: 'center',
  },

  // Heart
  heartRow: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  heartBtn: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    paddingVertical: 4,
    paddingHorizontal: 2,
  },
  heartEmoji: {
    fontSize: 26,
  },
  heartCount: {
    fontSize: 15,
    fontWeight: '700',
    color: '#ff6b6b',
  },
  thumbCount: {
    fontSize: 15,
    fontWeight: '700',
    color: 'rgba(255,255,255,0.9)',
  },
});
