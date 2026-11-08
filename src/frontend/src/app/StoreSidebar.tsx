import type { Dashboard, Section } from '../shared/types'

type StoreSidebarProps = {
  section: Section
  roles: string[]
  dashboard?: Dashboard
  canViewOrders: boolean
  onNavigate: (section: Section) => void
  onSignOut: () => void
}

export function StoreSidebar({ section, roles, dashboard, canViewOrders, onNavigate, onSignOut }: StoreSidebarProps) {
  const sections: Section[] = ['Overview', 'Products', ...(canViewOrders ? ['Orders' as const] : [])]
  return (
    <aside className="sidebar">
      <a className="brand" href="#" onClick={() => onNavigate('Overview')} aria-label="StoreOps home">
        <span className="brand-mark">s</span><span>store<span className="brand-light">ops</span></span>
      </a>
      <div className="workspace-label">WORKSPACE</div>
      <nav aria-label="Main navigation">
        {sections.map(item => (
          <button key={item} className={`nav-item ${section === item ? 'active' : ''}`} onClick={() => onNavigate(item)}>
            <span className="nav-icon">{item === 'Overview' ? '◫' : item === 'Products' ? '▦' : '↗'}</span>{item}
            {item === 'Orders' && dashboard?.pendingOrderCount ? <span className="nav-count">{dashboard.pendingOrderCount}</span> : null}
          </button>
        ))}
      </nav>
      <div className="sidebar-bottom">
        <div className="help-card"><span className="help-icon">?</span><div><strong>Need a hand?</strong><small>Visit the help center</small></div><span className="help-arrow">↗</span></div>
        <div className="profile">
          <div className="avatar">{roles[0]?.slice(0, 2).toUpperCase() ?? 'SO'}</div>
          <div className="profile-copy"><strong>StoreOps user</strong><small>{roles.join(', ')}</small></div>
          <button className="sign-out" onClick={onSignOut} aria-label="Sign out">Sign out</button>
        </div>
      </div>
    </aside>
  )
}
