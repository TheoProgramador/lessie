export interface NavigationItem {
  id: string;
  title: string;
  type: 'item' | 'collapse' | 'group';
  translate?: string;
  icon?: string;
  hidden?: boolean;
  adminOnly?: boolean;
  url?: string;
  classes?: string;
  exactMatch?: boolean;
  external?: boolean;
  target?: boolean;
  breadcrumbs?: boolean;
  children?: NavigationItem[];
}

export const NavigationItems: NavigationItem[] = [
  {
    id: 'navigation',
    title: 'Navigation',
    type: 'group',
    icon: 'icon-navigation',
    children: [
      {
        id: 'dashboard',
        title: 'Dashboard',
        type: 'item',
        url: '/dashboard',
        icon: 'feather icon-home',
        classes: 'nav-item'
      },
      {
        id: 'credits',
        title: 'Comprar creditos',
        type: 'item',
        url: '/credits',
        icon: 'feather icon-credit-card',
        classes: 'nav-item'
      },
      {
        id: 'people-discovery',
        title: 'People Discovery',
        type: 'collapse',
        icon: 'feather icon-users',
        children: [
          {
            id: 'people-search',
            title: 'People Search',
            type: 'item',
            url: '/people-discovery',
            icon: 'feather icon-users',
            classes: 'nav-item'
          },
          {
            id: 'post-discovery',
            title: 'Post Search',
            type: 'item',
            url: '/people-discovery/posts',
            icon: 'feather icon-file-text',
            classes: 'nav-item'
          },
          {
            id: 'job-discovery',
            title: 'Job Search',
            type: 'item',
            url: '/people-discovery/jobs',
            icon: 'feather icon-briefcase',
            classes: 'nav-item'
          }
        ]
      },
      {
        id: 'opportunity-discovery',
        title: 'Opportunity Discovery',
        type: 'item',
        url: '/opportunity-discovery',
        icon: 'feather icon-search',
        classes: 'nav-item'
      },
      {
        id: 'chatbot',
        title: 'Chat',
        type: 'item',
        url: '/chatbot',
        icon: 'feather icon-message-square',
        classes: 'nav-item',
        adminOnly: true
      },
      {
        id: 'resume-improvements',
        title: 'Melhorias do curriculo',
        type: 'item',
        url: '/resume-improvements',
        icon: 'feather icon-file-text',
        classes: 'nav-item'
      },
      {
        id: 'interview-analysis',
        title: 'Analise de entrevista',
        type: 'item',
        url: '/interview-analysis',
        icon: 'feather icon-mic',
        classes: 'nav-item'
      },
    ]
  }
];
