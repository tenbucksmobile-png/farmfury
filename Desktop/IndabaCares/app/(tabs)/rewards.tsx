import React, { useRef, useState, memo } from 'react';
import {
  Animated, View, Text, FlatList, StyleSheet,
  Modal, TouchableWithoutFeedback, TouchableOpacity, Alert,
} from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { router } from 'expo-router';
import { Pressable } from 'react-native';
import { useRewards, useEmployeePoints } from '@/hooks/use-rewards';
import { useEmployee } from '@/providers/EmployeeContext';
import { OptimizedImage } from '@/components/ui/OptimizedImage';
import { Skeleton } from '@/components/ui/Skeleton';
import type { Reward } from '@/api/reward-service';

const PURPLE = '#7B1FA2';

// ── Rewards skeleton (PERF-08) ────────────────────────────────────────────────
function RewardsSkeleton() {
  return (
    <View style={{ flexDirection: 'row', flexWrap: 'wrap', paddingHorizontal: 20, gap: 12, marginTop: 8 }}>
      {Array.from({ length: 6 }).map((_, i) => (
        <Skeleton key={i} width="47%" height={160} borderRadius={16} />
      ))}
    </View>
  );
}

// ── Flip card (PERF-07: memo prevents re-renders from parent state changes) ───
const RewardCard = memo(function RewardCard({ item, myPoints }: { item: Reward; myPoints: number }) {
  const flipAnim = useRef(new Animated.Value(0)).current;
  const [flipped, setFlipped] = useState(false);
  const [confirming, setConfirming] = useState(false);

  const isHotel   = item.category === 'hotel';
  const canAfford  = myPoints >= item.points_required;
  const outOfStock = item.stock <= 0;

  const imageUri  = item.image_url ?? null;

  const handleFlip = () => {
    const toValue = flipped ? 0 : 1;
    // scaleX + opacity: no rotateY (3D transforms cause Android Fabric crashes).
    // scaleX and opacity are safe with useNativeDriver: true on all platforms.
    Animated.spring(flipAnim, { toValue, friction: 8, tension: 50, useNativeDriver: true }).start();
    setFlipped(v => !v);
  };

  // Card "folds" to scaleX=0 at midpoint then unfolds — looks like a flip
  const scaleX       = flipAnim.interpolate({ inputRange: [0, 0.5, 1], outputRange: [1, 0.02, 1] });
  const frontOpacity = flipAnim.interpolate({ inputRange: [0, 0.45, 0.55, 1], outputRange: [1, 1, 0, 0] });
  const backOpacity  = flipAnim.interpolate({ inputRange: [0, 0.45, 0.55, 1], outputRange: [0, 0, 1, 1] });

  return (
    <View style={[s.cardContainer, outOfStock && s.cardDisabled]}>

      {/* ── Animated wrapper: scaleX drives the fold, opacity switches faces ── */}
      <Animated.View
        pointerEvents="none"
        style={[StyleSheet.absoluteFillObject, { transform: [{ scaleX }] }]}
      >

        {/* ── FRONT ── */}
        <Animated.View style={[s.cardFace, { opacity: frontOpacity }]}>
          {isHotel ? (
            <>
              <View style={s.cardTop}>
                <Text style={s.cardTitle} numberOfLines={2}>{item.title}</Text>
              </View>
              <View style={s.divider} />
              {imageUri ? (
                <OptimizedImage uri={imageUri} style={s.photoImg} contentFit="cover" />
              ) : (
                <View style={[s.photoImg, s.imagePlaceholder]} />
              )}
            </>
          ) : (
            <>
              <View style={s.cardTop}>
                <Text style={s.cardTitle} numberOfLines={2}>{item.title}</Text>
              </View>
              <View style={s.divider} />
              <View style={s.logoWrap}>
                {imageUri ? (
                  <OptimizedImage uri={imageUri} style={s.brandLogo} contentFit="contain" />
                ) : (
                  <View style={s.imagePlaceholder} />
                )}
              </View>
            </>
          )}
          {/* Points — bottom-left overlay on image */}
          <View style={s.pointsOverlay}>
            <Ionicons name="cash-outline" size={12} color="#16a34a" />
            <Text style={s.pointsOverlayTxt}> {item.points_required}</Text>
          </View>
        </Animated.View>

        {/* ── BACK ── */}
        <Animated.View
          style={[s.cardFace, s.cardBack, { opacity: backOpacity }]}
        >
          <View style={s.backHeader}>
            <Text style={s.backHeading} numberOfLines={2}>{item.title}</Text>
          </View>
          <View style={s.divider} />
          <View style={s.backBody}>
            <Text style={s.backDesc}>{item.description ?? ''}</Text>
          </View>
          {!outOfStock && (
            canAfford
              ? <Text style={s.canAfford}>✓ Can redeem</Text>
              : <Text style={s.deficit}><Text style={s.deficitCross}>✗ </Text>Need {item.points_required - myPoints} more pts</Text>
          )}
        </Animated.View>

      </Animated.View>{/* end scaleX wrapper */}

      {/* ── TOUCH LAYER — flat siblings, no nesting ── */}

      {/* Flip zone */}
      <TouchableOpacity
        style={[StyleSheet.absoluteFillObject, { elevation: 1 }]}
        activeOpacity={1}
        onPress={handleFlip}
        disabled={outOfStock}
      />

      {/* Redeem tick */}
      {!outOfStock && !flipped && (
        <TouchableOpacity
          style={[s.redeemBtn, !canAfford && s.redeemBtnDim, { elevation: 10 }]}
          onPress={() => {
            if (!canAfford) {
              Alert.alert(
                'Insufficient Balance',
                'You do not have enough to redeem this reward at this time.',
                [{ text: 'OK' }],
              );
              return;
            }
            setConfirming(true);
          }}
          activeOpacity={0.7}
          hitSlop={8}
        >
          <Ionicons name="checkmark" size={20} color="#fff" />
        </TouchableOpacity>
      )}

      {/* Confirmation modal */}
      <Modal visible={confirming} transparent animationType="fade" onRequestClose={() => setConfirming(false)}>
        <View style={s.confirmOverlay}>
          <View style={s.confirmBox}>
            <View style={s.confirmIconRow}>
              <View style={[s.confirmIconWrap, isHotel && s.confirmIconWrapPhoto]}>
                {imageUri ? (
                  <OptimizedImage
                    uri={imageUri}
                    style={s.confirmLogo}
                    contentFit={isHotel ? 'cover' : 'contain'}
                  />
                ) : (
                  <View style={[s.confirmLogo, s.imagePlaceholder]} />
                )}
              </View>
            </View>
            <Text style={s.confirmTitle}>Redeem Reward</Text>
            <Text style={s.confirmMsg}>
              You are about to redeem{'\n'}
              <Text style={s.confirmBrand}>"{item.title}"</Text>
              {'\n'}for{'  '}
            </Text>
            <View style={s.confirmPtsRow}>
              <Ionicons name="cash-outline" size={16} color="#16a34a" />
              <Text style={s.confirmPts}>{item.points_required} pts</Text>
            </View>
            <View style={s.confirmBtns}>
              <TouchableOpacity style={s.btnBack} onPress={() => setConfirming(false)} activeOpacity={0.8}>
                <Text style={s.btnBackTxt}>Back</Text>
              </TouchableOpacity>
              <TouchableOpacity
                style={s.btnConfirm}
                activeOpacity={0.8}
                onPress={() => {
                  setConfirming(false);
                  router.push(`/(screens)/reward/${item.id}` as any);
                }}
              >
                <Text style={s.btnConfirmTxt}>Confirm</Text>
              </TouchableOpacity>
            </View>
          </View>
        </View>
      </Modal>

    </View>
  );
});

// ── Screen ────────────────────────────────────────────────────────────────────
export default function RewardsScreen() {
  const [menuOpen,  setMenuOpen]  = useState(false);
  const [activeTab, setActiveTab] = useState<'retail' | 'hotel'>('hotel');

  const { employee }                        = useEmployee();
  const { data: allRewards = [], isLoading } = useRewards();
  const { data: myPoints = 0 }              = useEmployeePoints();

  const currentRewards = allRewards.filter(r => r.category === activeTab);
  // Pair rewards into rows of 2 for the grid layout
  const rows: Reward[][] = [];
  for (let i = 0; i < currentRewards.length; i += 2) {
    rows.push(currentRewards.slice(i, i + 2));
  }

  return (
    <SafeAreaView style={s.safe} edges={['top']}>

      {/* ── Header ── */}
      <View style={s.header}>

        {/* Top row: menu + balance */}
        <View style={s.headerTopRow}>
          <View>
            <Pressable style={s.iconBtn} onPress={() => setMenuOpen(true)} hitSlop={8}>
              <Ionicons name="menu" size={24} color="#fff" />
            </Pressable>

            <Modal visible={menuOpen} transparent animationType="none" onRequestClose={() => setMenuOpen(false)}>
              <TouchableWithoutFeedback onPress={() => setMenuOpen(false)}>
                <View style={s.modalBackdrop}>
                  <TouchableWithoutFeedback>
                    <View style={s.dropdown}>
                      <Pressable style={s.dropdownItem} onPress={() => { setMenuOpen(false); router.push('/(screens)/orders' as any); }}>
                        <Ionicons name="time-outline" size={16} color={PURPLE} />
                        <Text style={s.dropdownText}>Pending Orders</Text>
                      </Pressable>
                      <View style={s.dropdownDivider} />
                      <Pressable style={s.dropdownItem} onPress={() => { setMenuOpen(false); router.push('/(screens)/redeemed' as any); }}>
                        <Ionicons name="checkmark-circle-outline" size={16} color={PURPLE} />
                        <Text style={s.dropdownText}>Redeem History</Text>
                      </Pressable>
                      <View style={s.dropdownDivider} />
                      <Pressable style={s.dropdownItem} onPress={() => setMenuOpen(false)}>
                        <Ionicons name="information-circle-outline" size={16} color={PURPLE} />
                        <Text style={s.dropdownText}>How it Works</Text>
                      </Pressable>
                    </View>
                  </TouchableWithoutFeedback>
                </View>
              </TouchableWithoutFeedback>
            </Modal>
          </View>

          <View style={s.balanceCol}>
            <Text style={s.balanceLabel}>Reward Wallet Balance</Text>
            <View style={s.balanceRow}>
              <View style={s.starBox}>
                <Ionicons name="cash-outline" size={20} color="#34d399" />
              </View>
              <Text style={[s.balanceValue, { marginLeft: 10 }]}>{myPoints}</Text>
            </View>
          </View>
        </View>

      </View>

      {/* ── Pill tab selector ── */}
      <View style={s.tabPill}>
        <TouchableOpacity
          style={[s.tabBtn, activeTab === 'hotel' && s.tabBtnActive]}
          onPress={() => setActiveTab('hotel')}
          activeOpacity={0.8}
        >
          <Text style={[s.tabTxt, activeTab === 'hotel' && s.tabTxtActive]}>Hotel Rewards</Text>
        </TouchableOpacity>
        <TouchableOpacity
          style={[s.tabBtn, activeTab === 'retail' && s.tabBtnActive]}
          onPress={() => setActiveTab('retail')}
          activeOpacity={0.8}
        >
          <Text style={[s.tabTxt, activeTab === 'retail' && s.tabTxtActive]}>Marketplace</Text>
        </TouchableOpacity>
      </View>

      {/* ── Grid — FlatList for virtualization (PERF-07) ── */}
      {isLoading ? (
        <RewardsSkeleton />
      ) : currentRewards.length === 0 ? (
        <View style={s.empty}>
          <Ionicons name="gift-outline" size={48} color="#ddd6fe" />
          <Text style={s.emptyText}>No rewards available yet.</Text>
        </View>
      ) : (
        <FlatList
          data={rows}
          keyExtractor={(_, i) => String(i)}
          renderItem={({ item: row }) => (
            <View style={s.row}>
              {row.map((item) => (
                <RewardCard key={item.id} item={item} myPoints={myPoints} />
              ))}
              {row.length === 1 && <View style={s.cardContainer} />}
            </View>
          )}
          ListHeaderComponent={<View style={s.handle} />}
          contentContainerStyle={s.content}
          showsVerticalScrollIndicator={false}
          windowSize={5}
          maxToRenderPerBatch={4}
          initialNumToRender={4}
          removeClippedSubviews={false}
        />
      )}

    </SafeAreaView>
  );
}

const s = StyleSheet.create({
  safe:    { flex: 1, backgroundColor: '#f5f3ff' },
  content: { paddingHorizontal: 16, paddingBottom: 110, flexGrow: 1 },

  // Header
  header: {
    backgroundColor: PURPLE,
    borderBottomLeftRadius: 30,
    borderBottomRightRadius: 30,
    paddingHorizontal: 20,
    paddingTop: 14,
    paddingBottom: 30,
    flexDirection: 'column',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 6 },
    shadowOpacity: 0.2,
    shadowRadius: 12,
    elevation: 10,
  },
  headerTopRow:  { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', marginBottom: 4 },
  iconBtn:       { width: 40, height: 40, borderRadius: 10, backgroundColor: 'rgba(255,255,255,0.18)', alignItems: 'center', justifyContent: 'center' },
  balanceCol: {
    alignItems: 'flex-end',
    backgroundColor: 'rgba(255,255,255,0.12)',
    borderRadius: 16,
    borderWidth: 1,
    borderColor: 'rgba(255,255,255,0.25)',
    paddingHorizontal: 14,
    paddingVertical: 8,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.2,
    shadowRadius: 6,
    elevation: 4,
  },
  balanceLabel:  { fontSize: 10, fontWeight: '600', color: 'rgba(255,255,255,0.65)', letterSpacing: 0.5, marginBottom: 4, textTransform: 'uppercase' },
  balanceRow:    { flexDirection: 'row', alignItems: 'center' },
  starBox:       { alignItems: 'center', justifyContent: 'center' },
  balanceValue:  { fontSize: 26, fontWeight: 'bold', color: '#fff' },

  // Pill tabs
  tabPill:      { flexDirection: 'row', backgroundColor: '#8E24AA', borderRadius: 20, padding: 4, marginHorizontal: 20, marginTop: -22, zIndex: 10, elevation: 4 },
  tabBtn:       { flex: 1, alignItems: 'center', paddingVertical: 10, borderRadius: 16 },
  tabBtnActive: { backgroundColor: '#ffffff', shadowColor: '#000', shadowOffset: { width: 0, height: 2 }, shadowOpacity: 0.12, shadowRadius: 4, elevation: 3 },
  tabTxt:       { fontSize: 13, fontWeight: '700', color: 'rgba(255,255,255,0.75)' },
  tabTxtActive: { color: PURPLE },

  // Dropdown
  modalBackdrop:   { flex: 1 },
  dropdown:        { position: 'absolute', top: 100, left: 16, backgroundColor: '#fff', borderRadius: 14, minWidth: 200, shadowColor: '#000', shadowOffset: { width: 0, height: 4 }, shadowOpacity: 0.15, shadowRadius: 12, elevation: 8 },
  dropdownItem:    { flexDirection: 'row', alignItems: 'center', paddingHorizontal: 16, paddingVertical: 14 },
  dropdownText:    { marginLeft: 10, fontSize: 14, fontWeight: '600', color: '#1e1b4b' },
  dropdownDivider: { height: 1, backgroundColor: '#f1f5f9', marginHorizontal: 12 },

  handle: { width: 36, height: 4, borderRadius: 2, backgroundColor: '#ddd6fe', alignSelf: 'center', marginVertical: 14 },

  // Empty state
  empty:     { alignItems: 'center', justifyContent: 'center', paddingTop: 60, gap: 12 },
  emptyText: { fontSize: 14, color: '#94a3b8', fontWeight: '500' },

  // Grid
  row: { flexDirection: 'row', justifyContent: 'space-between', marginBottom: 12 },

  // Card container
  cardContainer: {
    flex: 1,
    aspectRatio: 0.72,
    marginHorizontal: 4,
  },
  cardDisabled: {
    opacity: 0.38,
  },

  // Shared face
  cardFace: {
    position: 'absolute',
    top: 0, left: 0, right: 0, bottom: 0,
    borderRadius: 16,
    backgroundColor: 'rgba(255,255,255,0.92)',
    borderWidth: 1.5,
    borderColor: '#000',
  },
  cardBack: { backgroundColor: '#faf5ff' },

  // Front
  logoWrap:      { flex: 1, alignItems: 'center', justifyContent: 'center', padding: 6 },
  brandLogo:     { width: '100%', height: '100%' },
  photoImg:      { flex: 1, width: '100%' },
  imagePlaceholder: { backgroundColor: '#e5e7eb', flex: 1 },
  backLogoWrap:  { width: 48, height: 36, borderRadius: 6, overflow: 'hidden', marginRight: 6, flexShrink: 0 },
  divider:       { height: 1, backgroundColor: 'rgba(0,0,0,0.1)', marginHorizontal: 8 },
  cardTop:       { paddingLeft: 8, paddingRight: 8, paddingTop: 6, paddingBottom: 4, alignItems: 'center' },
  cardTitle:     { fontSize: 11, fontWeight: '700', color: '#1e1b4b', lineHeight: 15, textAlign: 'center' },
  canAfford:     { fontSize: 9, fontWeight: '700', color: '#16a34a', textAlign: 'center', paddingBottom: 4 },
  deficit:       { fontSize: 9, color: '#ef4444', textAlign: 'center', paddingBottom: 4 },
  deficitCross:  { color: '#ef4444', fontWeight: '700' },

  // Points overlay
  pointsOverlay:    { position: 'absolute', bottom: 8, left: 8, flexDirection: 'row', alignItems: 'center', backgroundColor: 'rgba(255,255,255,0.92)', borderRadius: 20, paddingHorizontal: 8, paddingVertical: 4, borderWidth: 1, borderColor: 'rgba(22,163,74,0.25)' },
  pointsOverlayTxt: { fontSize: 13, fontWeight: '800', color: '#16a34a' },

  // Redeem button
  redeemBtn:    { position: 'absolute', bottom: 6, right: 6, width: 34, height: 34, borderRadius: 17, backgroundColor: PURPLE, alignItems: 'center', justifyContent: 'center' },
  redeemBtnDim: { backgroundColor: '#c4b5fd' },

  // Confirmation modal
  confirmOverlay:       { flex: 1, backgroundColor: 'rgba(0,0,0,0.55)', alignItems: 'center', justifyContent: 'center' },
  confirmBox:           { backgroundColor: '#fff', borderRadius: 20, padding: 24, width: '78%', alignItems: 'center' },
  confirmIconRow:       { marginBottom: 12, alignItems: 'center' },
  confirmIconWrap:      { width: 90, height: 72, borderRadius: 16, backgroundColor: '#fff', borderWidth: 1.5, borderColor: '#ede9fe', alignItems: 'center', justifyContent: 'center', overflow: 'hidden', padding: 8 },
  confirmIconWrapPhoto: { padding: 0 },
  confirmLogo:          { width: '100%', height: '100%' },
  confirmPtsRow:        { flexDirection: 'row', alignItems: 'center', gap: 5, marginBottom: 20 },
  confirmTitle:         { fontSize: 17, fontWeight: '700', color: '#1e1b4b', marginBottom: 10 },
  confirmMsg:           { fontSize: 13, color: '#374151', textAlign: 'center', lineHeight: 20, marginBottom: 6 },
  confirmBrand:         { fontWeight: '700', color: '#1e1b4b' },
  confirmPts:           { fontSize: 15, fontWeight: '700', color: '#16a34a' },
  confirmBtns:          { flexDirection: 'row', gap: 10, width: '100%' },
  btnBack:              { flex: 1, paddingVertical: 12, borderRadius: 12, borderWidth: 1.5, borderColor: '#e2e8f0', alignItems: 'center' },
  btnBackTxt:           { fontSize: 14, fontWeight: '600', color: '#64748b' },
  btnConfirm:           { flex: 1, paddingVertical: 12, borderRadius: 12, backgroundColor: PURPLE, alignItems: 'center' },
  btnConfirmTxt:        { fontSize: 14, fontWeight: '700', color: '#fff' },

  // Back face
  backHeader: { padding: 8, paddingBottom: 7, alignItems: 'center' },
  backHeading:{ fontSize: 10, fontWeight: '700', color: '#1e1b4b', lineHeight: 14, textAlign: 'center' },
  backBody:   { flex: 1, padding: 10 },
  backDesc:   { fontSize: 11, color: '#374151', lineHeight: 16 },
});
