'use client';

import { useState, useTransition } from 'react';
import { useRouter } from 'next/navigation';
import { toast } from 'sonner';
import { Upload, Search, Filter, Pencil, Trash2, UserPlus, KeyRound } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input }  from '@/components/ui/input';
import { Label }  from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/components/ui/dialog';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { HOTELS } from '@/lib/hotels';
import { toggleEmployeeStatus, updateEmployee, deleteEmployee, createEmployee, resetEmployeePassword } from '@/app/actions/employees';
import { CsvImportDialog } from './csv-import-dialog';

interface Employee {
  id:                    string;
  employee_code:         string;
  full_name:             string;
  hotel:                 string;
  department:            string | null;
  position:              string | null;
  email:                 string | null;
  status:                string;
  points_balance:        number;
  reward_wallet_balance: number | null;
  is_manager:            boolean;
  created_at:            string;
}

const STATUS_CHIP: Record<string, string> = {
  active:   'bg-emerald-100 text-emerald-700',
  inactive: 'bg-slate-100  text-slate-600',
  pending:  'bg-amber-100  text-amber-700',
};

// ── Edit Dialog ───────────────────────────────────────────────────────────────

function EditEmployeeDialog({
  employee,
  onClose,
}: {
  employee: Employee;
  onClose:  () => void;
}) {
  const [isPending,      startTransition] = useTransition();
  const [fullName,       setFullName]     = useState(employee.full_name);
  const [department,     setDepartment]   = useState(employee.department   ?? '');
  const [position,       setPosition]     = useState(employee.position     ?? '');
  const [email,          setEmail]        = useState(employee.email        ?? '');
  const [dateOfBirth,    setDateOfBirth]  = useState((employee as any).date_of_birth ?? '');
  const [startDate,      setStartDate]    = useState((employee as any).start_date    ?? '');
  const [isManager,      setIsManager]    = useState(employee.is_manager ?? false);
  const [pointsBalance,       setPointsBalance]       = useState(String(employee.points_balance        ?? 0));
  const [walletBalance,       setWalletBalance]       = useState(String(employee.reward_wallet_balance ?? 0));

  function handleSave() {
    if (!fullName.trim()) {
      toast.error('Name is required');
      return;
    }
    startTransition(async () => {
      try {
        await updateEmployee(employee.id, {
          full_name:      fullName,
          department:     department    || null,
          position:       position      || null,
          email:          email         || null,
          date_of_birth:  dateOfBirth   || null,
          start_date:     startDate     || null,
          is_manager:     isManager,
          points_balance:        parseInt(pointsBalance, 10)  || 0,
          reward_wallet_balance: parseInt(walletBalance, 10)  || 0,
        });
        toast.success('Employee updated');
        onClose();
      } catch (err: any) {
        toast.error(err.message);
      }
    });
  }

  return (
    <Dialog open onOpenChange={(o) => { if (!o) onClose(); }}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>Edit Employee</DialogTitle>
          <p className="text-sm text-muted-foreground font-mono">{employee.employee_code} · {employee.hotel}</p>
        </DialogHeader>

        <div className="space-y-4 py-2">
          <div className="space-y-1.5">
            <Label>Full Name <span className="text-red-500">*</span></Label>
            <Input value={fullName} onChange={(e) => setFullName(e.target.value)} />
          </div>
          <div className="space-y-1.5">
            <Label>Department</Label>
            <Input value={department} onChange={(e) => setDepartment(e.target.value)} placeholder="e.g. Front Office" />
          </div>
          <div className="space-y-1.5">
            <Label>Position</Label>
            <Input value={position} onChange={(e) => setPosition(e.target.value)} placeholder="e.g. Receptionist" />
          </div>
          <div className="space-y-1.5">
            <Label>Email</Label>
            <Input value={email} onChange={(e) => setEmail(e.target.value)} placeholder="employee@hotel.com" type="email" />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label>Date of Birth</Label>
              <Input value={dateOfBirth} onChange={(e) => setDateOfBirth(e.target.value)} type="date" />
            </div>
            <div className="space-y-1.5">
              <Label>Start Date</Label>
              <Input value={startDate} onChange={(e) => setStartDate(e.target.value)} type="date" />
            </div>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label>Points Balance</Label>
              <Input
                type="number"
                min="0"
                value={pointsBalance}
                onChange={(e) => setPointsBalance(e.target.value)}
              />
              <p className="text-xs text-muted-foreground">Recognition points (earned by being recognised).</p>
            </div>
            <div className="space-y-1.5">
              <Label>Reward Wallet (RP)</Label>
              <Input
                type="number"
                min="0"
                value={walletBalance}
                onChange={(e) => setWalletBalance(e.target.value)}
              />
              <p className="text-xs text-muted-foreground">Points used to redeem rewards.</p>
            </div>
          </div>
          <label className="flex cursor-pointer items-center gap-3 rounded-md border px-3 py-2.5">
            <input
              type="checkbox"
              checked={isManager}
              onChange={(e) => setIsManager(e.target.checked)}
              className="h-4 w-4 accent-violet-600"
            />
            <div>
              <p className="text-sm font-medium">Management</p>
              <p className="text-xs text-muted-foreground">Appears under the Management tab on the leaderboard</p>
            </div>
          </label>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={isPending}>Cancel</Button>
          <Button onClick={handleSave} disabled={isPending}>
            {isPending ? 'Saving…' : 'Save Changes'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// ── Add Employee Dialog ───────────────────────────────────────────────────────

function AddEmployeeDialog({ onClose }: { onClose: () => void }) {
  const [isPending,    startTransition] = useTransition();
  const [fullName,     setFullName]     = useState('');
  const [employeeCode, setEmployeeCode] = useState('');
  const [hotel,        setHotel]        = useState('');
  const [department,   setDepartment]   = useState('');
  const [position,     setPosition]     = useState('');
  const [email,        setEmail]        = useState('');
  const [dateOfBirth,  setDateOfBirth]  = useState('');
  const [startDate,    setStartDate]    = useState('');
  const [isManager,    setIsManager]    = useState(false);

  function handleSave() {
    if (!fullName.trim())     { toast.error('Full name is required');     return; }
    if (!employeeCode.trim()) { toast.error('Employee code is required'); return; }
    if (!hotel)               { toast.error('Hotel is required');         return; }

    startTransition(async () => {
      try {
        await createEmployee({
          full_name:     fullName,
          employee_code: employeeCode,
          hotel,
          department:    department   || null,
          position:      position     || null,
          email:         email        || null,
          date_of_birth: dateOfBirth  || null,
          start_date:    startDate    || null,
          is_manager:    isManager,
        });
        toast.success('Employee created');
        onClose();
      } catch (err: any) {
        toast.error(err.message);
      }
    });
  }

  return (
    <Dialog open onOpenChange={(o) => { if (!o) onClose(); }}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>Add Employee</DialogTitle>
        </DialogHeader>

        <div className="space-y-4 py-2">
          <div className="space-y-1.5">
            <Label>Full Name <span className="text-red-500">*</span></Label>
            <Input value={fullName} onChange={(e) => setFullName(e.target.value)} placeholder="Jane Smith" />
          </div>
          <div className="space-y-1.5">
            <Label>Employee Code <span className="text-red-500">*</span></Label>
            <Input
              value={employeeCode}
              onChange={(e) => setEmployeeCode(e.target.value)}
              placeholder="EMP001"
              className="font-mono uppercase"
            />
            <p className="text-xs text-muted-foreground">Letters, numbers, hyphens and underscores only. Stored in uppercase.</p>
          </div>
          <div className="space-y-1.5">
            <Label>Hotel <span className="text-red-500">*</span></Label>
            <Select value={hotel} onValueChange={setHotel}>
              <SelectTrigger><SelectValue placeholder="Select hotel…" /></SelectTrigger>
              <SelectContent>
                {HOTELS.map((h) => <SelectItem key={h} value={h}>{h}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1.5">
            <Label>Department</Label>
            <Input value={department} onChange={(e) => setDepartment(e.target.value)} placeholder="e.g. Front Office" />
          </div>
          <div className="space-y-1.5">
            <Label>Position</Label>
            <Input value={position} onChange={(e) => setPosition(e.target.value)} placeholder="e.g. Receptionist" />
          </div>
          <div className="space-y-1.5">
            <Label>Email</Label>
            <Input value={email} onChange={(e) => setEmail(e.target.value)} placeholder="jane.smith@hotel.com" type="email" />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label>Date of Birth</Label>
              <Input value={dateOfBirth} onChange={(e) => setDateOfBirth(e.target.value)} type="date" />
            </div>
            <div className="space-y-1.5">
              <Label>Start Date</Label>
              <Input value={startDate} onChange={(e) => setStartDate(e.target.value)} type="date" />
            </div>
          </div>
          <label className="flex cursor-pointer items-center gap-3 rounded-md border px-3 py-2.5">
            <input
              type="checkbox"
              checked={isManager}
              onChange={(e) => setIsManager(e.target.checked)}
              className="h-4 w-4 accent-violet-600"
            />
            <div>
              <p className="text-sm font-medium">Management</p>
              <p className="text-xs text-muted-foreground">Appears under the Management tab on the leaderboard</p>
            </div>
          </label>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={isPending}>Cancel</Button>
          <Button onClick={handleSave} disabled={isPending}>
            {isPending ? 'Creating…' : 'Create Employee'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// ── Main Component ────────────────────────────────────────────────────────────

export function EmployeesClient({
  employees,
  selectedHotel,
}: {
  employees:      Employee[];
  selectedHotel?: string;
}) {
  const router = useRouter();
  const [isPending, startTransition] = useTransition();
  const [search,       setSearch]      = useState('');
  const [importOpen,   setImportOpen]  = useState(false);
  const [addOpen,      setAddOpen]     = useState(false);
  const [editTarget,   setEditTarget]  = useState<Employee | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<Employee | null>(null);
  const [resetTarget,  setResetTarget]  = useState<Employee | null>(null);

  // ── Filtering ──────────────────────────────────────────────────────────────

  const filtered = employees.filter(
    (e) =>
      e.full_name.toLowerCase().includes(search.toLowerCase()) ||
      e.employee_code.toLowerCase().includes(search.toLowerCase()) ||
      (e.department ?? '').toLowerCase().includes(search.toLowerCase()),
  );

  function handleHotelFilter(val: string) {
    const url = new URLSearchParams();
    if (val !== 'all') url.set('hotel', val);
    router.push(`/employees${url.toString() ? '?' + url.toString() : ''}`);
  }

  function handleToggle(id: string, status: string) {
    startTransition(async () => {
      try {
        await toggleEmployeeStatus(id, status);
        toast.success('Status updated');
      } catch (err: any) {
        toast.error(err.message);
      }
    });
  }

  // ── Render ─────────────────────────────────────────────────────────────────

  return (
    <>
      {/* Toolbar */}
      <div className="flex flex-wrap items-center gap-3">
        <div className="relative min-w-48 flex-1">
          <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            placeholder="Search by name, code, or department…"
            className="pl-9"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>

        <Select value={selectedHotel ?? 'all'} onValueChange={handleHotelFilter}>
          <SelectTrigger className="w-52">
            <Filter className="mr-2 h-4 w-4" />
            <SelectValue placeholder="All hotels" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All Hotels</SelectItem>
            {HOTELS.map((h) => (
              <SelectItem key={h} value={h}>{h}</SelectItem>
            ))}
          </SelectContent>
        </Select>

        <Button variant="outline" onClick={() => setAddOpen(true)}>
          <UserPlus className="mr-2 h-4 w-4" />
          Add Employee
        </Button>
        <Button onClick={() => setImportOpen(true)}>
          <Upload className="mr-2 h-4 w-4" />
          Import CSV
        </Button>
      </div>

      {/* Row count */}
      <p className="text-sm text-muted-foreground">
        {filtered.length.toLocaleString()} employee{filtered.length !== 1 ? 's' : ''}
      </p>

      {/* Table */}
      <div className="rounded-lg border bg-card">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Code</TableHead>
              <TableHead>Name</TableHead>
              <TableHead>Hotel</TableHead>
              <TableHead>Department</TableHead>
              <TableHead>Position</TableHead>
              <TableHead>Email</TableHead>
              <TableHead className="text-right">Points</TableHead>
              <TableHead>Role</TableHead>
              <TableHead>Status</TableHead>
              <TableHead />
            </TableRow>
          </TableHeader>
          <TableBody>
            {filtered.length === 0 && (
              <TableRow>
                <TableCell colSpan={10} className="py-10 text-center text-muted-foreground">
                  No employees found.
                </TableCell>
              </TableRow>
            )}
            {filtered.map((emp) => (
              <TableRow key={emp.id}>
                <TableCell className="font-mono text-xs">{emp.employee_code}</TableCell>
                <TableCell className="font-medium">{emp.full_name}</TableCell>
                <TableCell className="text-sm text-muted-foreground">{emp.hotel}</TableCell>
                <TableCell className="text-sm text-muted-foreground">{emp.department ?? '—'}</TableCell>
                <TableCell className="text-sm text-muted-foreground">{emp.position  ?? '—'}</TableCell>
                <TableCell className="text-sm text-muted-foreground">{emp.email ?? '—'}</TableCell>
                <TableCell className="text-right font-semibold">{emp.points_balance ?? 0}</TableCell>
                <TableCell>
                  {emp.is_manager ? (
                    <span className="rounded-full bg-violet-100 px-2 py-0.5 text-xs font-semibold text-violet-700">
                      Management
                    </span>
                  ) : (
                    <span className="text-xs text-muted-foreground">Employee</span>
                  )}
                </TableCell>
                <TableCell>
                  <span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${STATUS_CHIP[emp.status] ?? 'bg-slate-100 text-slate-600'}`}>
                    {emp.status}
                  </span>
                </TableCell>
                <TableCell className="text-right">
                  <div className="flex items-center justify-end gap-1">
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => setEditTarget(emp)}
                    >
                      <Pencil className="h-3.5 w-3.5" />
                    </Button>
                    <Button
                      variant="ghost"
                      size="sm"
                      disabled={isPending}
                      onClick={() => handleToggle(emp.id, emp.status)}
                    >
                      {emp.status === 'active' ? 'Deactivate' : 'Activate'}
                    </Button>
                    <Button
                      variant="ghost"
                      size="sm"
                      title="Reset password"
                      onClick={() => setResetTarget(emp)}
                    >
                      <KeyRound className="h-3.5 w-3.5" />
                    </Button>
                    <Button
                      variant="ghost"
                      size="sm"
                      className="text-destructive hover:text-destructive"
                      onClick={() => setDeleteTarget(emp)}
                    >
                      <Trash2 className="h-3.5 w-3.5" />
                    </Button>
                  </div>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>

      {/* Import dialog */}
      <CsvImportDialog
        open={importOpen}
        onClose={() => setImportOpen(false)}
      />

      {/* Add employee dialog */}
      {addOpen && (
        <AddEmployeeDialog
          onClose={() => {
            setAddOpen(false);
            router.refresh();
          }}
        />
      )}

      {/* Edit dialog */}
      {editTarget && (
        <EditEmployeeDialog
          employee={editTarget}
          onClose={() => {
            setEditTarget(null);
            router.refresh();
          }}
        />
      )}

      {/* Reset password confirmation */}
      <AlertDialog open={!!resetTarget} onOpenChange={(o) => { if (!o) setResetTarget(null); }}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Reset password?</AlertDialogTitle>
            <AlertDialogDescription>
              This will clear <strong>{resetTarget?.full_name}</strong>'s password and log them out of all devices.
              They will need to re-register using the <strong>Register</strong> tab in the mobile app.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              onClick={() => {
                if (!resetTarget) return;
                startTransition(async () => {
                  try {
                    await resetEmployeePassword(resetTarget.id);
                    toast.success('Password reset — employee can now re-register');
                    setResetTarget(null);
                  } catch (err: any) {
                    toast.error(err.message);
                    setResetTarget(null);
                  }
                });
              }}
            >
              Reset Password
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* Delete confirmation */}
      <AlertDialog open={!!deleteTarget} onOpenChange={(o) => { if (!o) setDeleteTarget(null); }}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete employee?</AlertDialogTitle>
            <AlertDialogDescription>
              This will permanently delete <strong>{deleteTarget?.full_name}</strong> ({deleteTarget?.employee_code}).
              This action cannot be undone.{' '}
              If this employee has existing activity, deletion will be blocked — use Deactivate instead.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
              onClick={() => {
                if (!deleteTarget) return;
                startTransition(async () => {
                  try {
                    await deleteEmployee(deleteTarget.id);
                    toast.success('Employee deleted');
                    setDeleteTarget(null);
                    router.refresh();
                  } catch (err: any) {
                    toast.error(err.message);
                    setDeleteTarget(null);
                  }
                });
              }}
            >
              Delete
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
