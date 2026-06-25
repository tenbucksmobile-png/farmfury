'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { cn } from '@/lib/utils';
import {
  LayoutDashboard,
  Users,
  Gift,
  ClipboardList,
  BarChart3,
  Megaphone,
  Zap,
  Heart,
  SmilePlus,
  ChevronLeft,
  Rss,
  type LucideIcon,
} from 'lucide-react';
import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { useAuth } from '@/hooks/use-auth';

interface NavItem {
  label: string;
  href:  string;
  icon:  LucideIcon;
}

interface NavSection {
  title: string;
  items: NavItem[];
}

const NAV: NavSection[] = [
  {
    title: 'Main',
    items: [
      { label: 'Dashboard',     href: '/',               icon: LayoutDashboard },
    ],
  },
  {
    title: 'Manage',
    items: [
      { label: 'Employees',     href: '/employees',      icon: Users           },
      { label: 'Rewards',       href: '/rewards',        icon: Gift            },
      { label: 'Redemptions',   href: '/redemptions',    icon: ClipboardList   },
      { label: 'Campaigns',     href: '/campaigns',      icon: Zap             },
      { label: 'Indaba Cares',  href: '/initiatives',    icon: Heart           },
    ],
  },
  {
    title: 'Reporting',
    items: [
      { label: 'Analytics',     href: '/analytics',      icon: BarChart3       },
      { label: 'Mood Board',    href: '/mood',           icon: SmilePlus       },
    ],
  },
  {
    title: 'Communication',
    items: [
      { label: 'Channel',       href: '/channel',        icon: Rss             },
      { label: 'Notifications', href: '/notifications',  icon: Megaphone       },
    ],
  },
];

const CHANNEL_ONLY_NAV: NavSection[] = [
  {
    title: 'Channel',
    items: [
      { label: 'Channel', href: '/channel', icon: Rss },
    ],
  },
];

export function Sidebar() {
  const pathname         = usePathname();
  const [collapsed, setCollapsed] = useState(false);
  const { isChannelOnly, adminHotel } = useAuth();

  const nav = isChannelOnly ? CHANNEL_ONLY_NAV : NAV;

  return (
    <aside
      className={cn(
        'flex h-screen flex-col border-r bg-card transition-all duration-200',
        collapsed ? 'w-16' : 'w-60',
      )}
    >
      {/* Logo */}
      <div className="flex h-14 items-center justify-between border-b px-3">
        {!collapsed && (
          <div className="flex items-center gap-2">
            <div className="flex h-7 w-7 items-center justify-center rounded-lg bg-violet-600">
              <span className="text-xs font-bold text-white">IC</span>
            </div>
            <div className="min-w-0">
              <p className="text-sm font-bold tracking-tight leading-none">IndabaCares</p>
              {isChannelOnly && adminHotel && (
                <p className="truncate text-[10px] text-muted-foreground">{adminHotel}</p>
              )}
            </div>
          </div>
        )}
        <Button
          variant="ghost"
          size="icon"
          className="ml-auto h-8 w-8 shrink-0"
          onClick={() => setCollapsed((c) => !c)}
        >
          <ChevronLeft
            className={cn('h-4 w-4 transition-transform', collapsed && 'rotate-180')}
          />
        </Button>
      </div>

      {/* Navigation */}
      <nav className="flex-1 space-y-5 overflow-y-auto px-2 py-4">
        {nav.map((section) => (
          <div key={section.title}>
            {!collapsed && (
              <p className="mb-1 px-3 text-[10px] font-semibold uppercase tracking-widest text-muted-foreground">
                {section.title}
              </p>
            )}
            {section.items.map((item) => {
              const active =
                item.href === '/'
                  ? pathname === '/'
                  : pathname.startsWith(item.href);
              const Icon = item.icon;

              return (
                <Link
                  key={item.href}
                  href={item.href}
                  title={collapsed ? item.label : undefined}
                  className={cn(
                    'flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium transition-colors',
                    active
                      ? 'bg-violet-50 text-violet-700 dark:bg-violet-950 dark:text-violet-300'
                      : 'text-muted-foreground hover:bg-accent hover:text-foreground',
                  )}
                >
                  <Icon className="h-4 w-4 shrink-0" />
                  {!collapsed && <span>{item.label}</span>}
                </Link>
              );
            })}
          </div>
        ))}
      </nav>

      {!collapsed && (
        <div className="border-t px-4 py-3">
          <p className="text-[10px] text-muted-foreground">Admin Portal v1.0</p>
        </div>
      )}
    </aside>
  );
}
