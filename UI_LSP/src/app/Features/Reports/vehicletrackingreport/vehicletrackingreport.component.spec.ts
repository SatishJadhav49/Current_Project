import { ComponentFixture, TestBed } from '@angular/core/testing';

import { VehicletrackingreportComponent } from './vehicletrackingreport.component';

describe('VehicletrackingreportComponent', () => {
  let component: VehicletrackingreportComponent;
  let fixture: ComponentFixture<VehicletrackingreportComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [VehicletrackingreportComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(VehicletrackingreportComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
