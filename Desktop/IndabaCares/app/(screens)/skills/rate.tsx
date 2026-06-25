/**
 * Legacy route — redirects to the active skill recognition screen.
 *
 * This route previously used System 1 skill indicators (category → indicators
 * model). System 1 tables were removed in migration 029. The current skill
 * recognition flow lives in (screens)/skills/index.tsx which uses the
 * post-recognition system with skill-category badges.
 *
 * Deep links or bookmarks targeting /(screens)/skills/rate are silently
 * redirected on mount so no user sees a crash or blank screen.
 */

import { useEffect } from 'react';
import { router } from 'expo-router';

export default function RateRedirect() {
  useEffect(() => {
    router.replace('/(screens)/skills');
  }, []);

  return null;
}
