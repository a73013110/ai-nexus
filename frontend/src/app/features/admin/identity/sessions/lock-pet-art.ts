import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import type { PetLook } from './lock-pet-calendar';
import type { PetMood } from './lock-pet-physics';

const PLAIN: PetLook = { moment: null, hat: null, held: null };

/**
 * 小鎖的樣子：表情、帽子、手上的東西與被子都只看輸入，自己不動；位置、形變與眼睛看哪裡由使用的地方決定
 * （眼睛讀 `--pet-look`，例如 `2px -1px`）。鎖定畫面上會動的那隻與倒數島下掛著的那隻共用。
 */
@Component({
  selector: 'nx-lock-pet-art',
  templateUrl: './lock-pet-art.html',
  styleUrls: ['./lock-pet-art.scss', './lock-pet-props.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    'aria-hidden': 'true',
    '[attr.data-mood]': 'mood()',
    '[attr.data-moment]': 'look().moment',
    '[attr.data-hat]': 'look().hat',
    '[attr.data-held]': 'look().held',
    '[attr.data-open]': 'open() ? "" : null',
  },
})
export class LockPetArt {
  readonly mood = input<PetMood>('idle');
  readonly look = input<PetLook>(PLAIN);
  /** 鎖扣彈開：解鎖成功時，和膠囊裡的鎖扣同一刻打開。 */
  readonly open = input(false);
}
